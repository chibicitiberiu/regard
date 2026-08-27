using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using Quartz.AspNetCore;
using Regard.Backend.Hubs;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Backend.DB;
using Regard.Backend.Middleware;
using Regard.Backend.Jobs;
using Regard.Backend.Common.Providers;
using Regard.Backend.Common.Services;
using Regard.Backend.Providers.Rss;
using Regard.Backend.Providers.YouTubeDL;
using Regard.Backend.Downloader;
using Regard.Backend.Thumbnails;
using Regard.Backend.Configuration;
using Regard.Backend.Logging;
using Regard.Backend.Metadata;

namespace Regard.Backend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            // Setup database: SQLite is the default (no configuration required);
            // SQL Server is used only when its connection string is explicitly set.
            if (Configuration.GetConnectionString("SqlServer") != null)
                services.AddDbContext<DataContext, SQLServerDataContext>();
            else
                services.AddDbContext<DataContext, SQLiteDataContext>();

            // Messaging
            services.AddSignalR();
            services.AddResponseCompression(opts =>
            {
                opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/octet-stream" });
            });
            services.AddScoped<MessagingService>();

            // Authentication and security
            services.AddIdentity<UserAccount, IdentityRole>()
                .AddEntityFrameworkStores<DataContext>()
                .AddDefaultTokenProviders();

            // Persist DataProtection keys on the data volume so Identity/antiforgery tokens
            // survive container recreation (the default keeps them in an ephemeral ~/.aspnet dir).
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Configuration["DataDirectory"] ?? "Data", "DataProtection-Keys")))
                .SetApplicationName("Regard");

            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                //options.ClaimsIdentity.UserIdClaimType = ClaimTypes.Name;
            });

            // Resolve the JWT signing secret (generates + persists one when unset or still the
            // insecure shipped default) and share it with the token signer (AuthController).
            var jwtSecret = JwtSecretProvider.Resolve(Configuration);
            services.AddSingleton(new JwtSecretProvider(jwtSecret));

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("Admin", policy => policy.RequireRole(UserRoles.Admin));
            });

            // Cors
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(builder =>
                {
                    builder.AllowAnyOrigin();
                    builder.AllowAnyMethod();
                    builder.AllowAnyHeader();
                });
            });

            // Options
            services.AddScoped<IOptionManager, OptionManager>();
            services.AddSingleton(typeof(IOptionCache<>), typeof(OptionCache<>));

            // Scheduler
            services.AddQuartz(q =>
            {
                // Scoped job factory is the default in modern Quartz; the old
                // UseMicrosoftDependencyInjectionScopedJobFactory() call was removed.
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                // Serialize jobs: the SQLite-backed store doesn't tolerate many concurrent
                // writers, and one-at-a-time downloads are fine for a personal media server.
                q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 1);

                // Synchronize job
                q.ScheduleJob<InitJob>(trigger => trigger.StartNow());
            });
            services.AddQuartzServer(opts =>
            {
                opts.WaitForJobsToComplete = true;
            });
            services.AddScoped<RegardScheduler>();
            services.AddSingleton<JobTrackerService>();
            services.AddSingleton<UserLogger>();

            // Bridges job/message events to SignalR clients (live progress + toasts).
            services.AddHostedService<JobNotificationBroadcaster>();

            services.AddScoped<DeleteFilesJob>();
            services.AddScoped<DeleteWatchedFilesJob>();
            services.AddScoped<DeleteSubscriptionFilesJob>();
            services.AddScoped<DeleteSubscriptionFolderFilesJob>();
            services.AddScoped<DownloadVideoJob>();
            services.AddScoped<FetchThumbnailsJob>();
            services.AddScoped<InitJob>();
            services.AddScoped<JellyfinSyncJob>();
            services.AddScoped<SynchronizeJob>();
            services.AddScoped<YoutubeDLUpdateJob>();

            // Jellyfin watched-sync client (introduces IHttpClientFactory)
            services.AddHttpClient<Jellyfin.IJellyfinClient, Jellyfin.JellyfinClient>();

            // Providers
            services.AddSingleton<IProvider, RssSubscriptionProvider>();
            services.AddSingleton<IProvider, YouTubeDLProvider>();
            services.AddSingleton<IProviderManager, ProviderManager>();

            // Others
            services.AddScoped<SubscriptionManager>();
            services.AddScoped<VideoManager>();
            services.AddSingleton<StorageManager>();
            services.AddSingleton<ThumbnailService>();
            services.AddScoped<MetadataService>();
            services.AddSingleton<IVideoStorageService, VideoStorageService>();
            services.AddScoped<IVideoDownloaderService, VideoDownloaderService>();
            services.AddSingleton<IYoutubeDlService, YoutubeDLService>();
            services.AddSingleton<ApiResponseFactory>();
            services.AddSingleton<ApiModelFactory>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
                              IWebHostEnvironment env,
                              DataContext dataContext,
                              StorageManager storageManager)
        {
            app.UseSignalRQueryStringAuth();

            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Serve the Blazor WASM UI. In the Docker image the frontend's published wwwroot is
            // copied into this app's wwwroot; UseBlazorFrameworkFiles serves the _framework assets.
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            // TLS is terminated by the reverse proxy in production; only redirect in development.
            if (env.IsDevelopment())
                app.UseHttpsRedirection();

            app.UseRouting();

            // CORS is only needed for the separate frontend dev-server; production serves the UI
            // same-origin, so no cross-origin API access is granted there.
            if (env.IsDevelopment())
                app.UseCors();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessagingHub>("/api/message_hub");
                // SPA fallback: unmatched non-file routes serve index.html. The {*path:nonfile}
                // constraint means extensioned paths (e.g. /thumbs/x.jpg) and the explicit /api/*
                // + hub routes are never swallowed.
                endpoints.MapFallbackToFile("index.html");
            });

            storageManager.Initialize(app);

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REGARD_MIGRATE")))
                ApplyMigrations(dataContext);
        }

        public void ApplyMigrations(DataContext dataContext)
        {
            Console.WriteLine("Applying migrations...");
            dataContext.Database.Migrate();
        }
    }
}
