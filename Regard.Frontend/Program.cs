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

            builder.Services.AddSingleton<AppState>();
            builder.Services.AddSingleton<SubscriptionManagerService>();
            builder.Services.AddSingleton<MessagingService>();
            builder.Services.AddScoped<AppController>();
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
