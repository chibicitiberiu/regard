using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            builder.Services.AddSingleton<MessagingService>();
            builder.Services.AddBlazoredLocalStorage();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<MainAppController>();

            // backend
            builder.Services.AddScoped<BackendHttpClient>();
            builder.Services.AddScoped<BackendService>();

            // authentication
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
            builder.Services.AddSingleton<AuthenticationService>();

            var host = builder.Build();
            await host.RunAsync();
        }
    }
}
