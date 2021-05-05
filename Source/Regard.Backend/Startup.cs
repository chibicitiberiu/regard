using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using Regard.Backend.Hubs;
using Regard.Backend.Model;
using Regard.Backend.Providers.YouTube;
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

            // Setup database
            if (Configuration.GetConnectionString("SqlServer") != null)
                services.AddDbContext<DataContext, SQLServerDataContext>();
            else if (Configuration.GetConnectionString("SQLite") != null)
                services.AddDbContext<DataContext, SQLiteDataContext>();
            else throw new ArgumentException("No supported database added!");

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

            services.Configure<IdentityOptions>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                //options.ClaimsIdentity.UserIdClaimType = ClaimTypes.Name;
            });

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
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JWT:Secret"]))
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
                q.UseMicrosoftDependencyInjectionScopedJobFactory();
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();
                q.UseDefaultThreadPool();

                // Synchronize job
                q.ScheduleJob<InitJob>(trigger => trigger.StartNow());
            });
            services.AddQuartzServer(opts =>
            {
                opts.WaitForJobsToComplete = true;
            });
            services.AddScoped<RegardScheduler>();

            services.AddScoped<DeleteFilesJob>();
            services.AddScoped<DeleteSubscriptionFilesJob>();
            services.AddScoped<DeleteSubscriptionFolderFilesJob>();
            services.AddScoped<DownloadVideoJob>();
            services.AddScoped<FetchThumbnailsJob>();
            services.AddScoped<InitJob>();
            services.AddScoped<SynchronizeJob>();
            services.AddScoped<YoutubeDLUpdateJob>();

            // Providers
            services.AddSingleton<IProvider, RssSubscriptionProvider>();
            services.AddSingleton<IProvider, YouTubeAPIProvider>();
            services.AddSingleton<IProvider, YouTubeDLProvider>();
            services.AddSingleton<IProviderManager, ProviderManager>();

            // Others
            services.AddScoped<SubscriptionManager>();
            services.AddScoped<VideoManager>();
            services.AddSingleton<StorageManager>();
            services.AddSingleton<ThumbnailService>();
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

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<MessagingHub>("/api/message_hub");
            });

            app.UseStaticFiles();

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
