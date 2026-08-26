using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Regard.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Regard.Frontend.Services;

namespace Regard.Frontend
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            // Default the backend origin to this app's own origin when BACKEND_URL is left empty
            // (the single-container deploy serves the API + UI from the same host). A non-empty
            // value from wwwroot/appsettings.json (e.g. the dev localhost URL) is preserved.
            if (string.IsNullOrEmpty(builder.Configuration["BACKEND_URL"]))
                builder.Configuration["BACKEND_URL"] = builder.HostEnvironment.BaseAddress;


            builder.Services.AddSingleton<AppState>();
            builder.Services.AddSingleton<SubscriptionManagerService>();
            builder.Services.AddSingleton<MessagingService>();
            builder.Services.AddSingleton<AppController>();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            // storage
            builder.Services.AddBlazoredLocalStorage();

            // backend
            builder.Services.AddScoped<BackendHttpClient>();
            builder.Services.AddScoped<BackendService>();

            // authentication
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
            builder.Services.AddSingleton<AuthenticationService>();

            builder.Services.AddTransient<Popper.Popper>();

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}
