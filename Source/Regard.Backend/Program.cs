using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;

namespace Regard.Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var logger = SetupLogger();

            try
            {
                logger.Info("Starting up...");
                var host = CreateHostBuilder(args).Build();

                // Log the actual bound URL(s) once the server is up, so it's visible no matter how
                // the app was launched (CLI, VS Code, a debugger) — not just via the run script.
                var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
                lifetime.ApplicationStarted.Register(() =>
                {
                    var addresses = host.Services.GetService<IServer>()?.Features
                        .Get<IServerAddressesFeature>()?.Addresses;
                    foreach (var address in addresses ?? Enumerable.Empty<string>())
                        logger.Info("Backend listening on {0}", address);
                });

                host.Run();
            }
            catch(Exception ex)
            {
                logger.Fatal(ex, "Shutdown caused by critical exception!");
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        private static Logger SetupLogger()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()  // so DataDirectory=/data (env) drives NLog's log path, not the appsettings default
                .Build();

            GlobalDiagnosticsContext.Set("DataDirectory", config["DataDirectory"]);

            return LogManager.Setup().GetCurrentClassLogger();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                })
                .UseNLog();
    }
}
