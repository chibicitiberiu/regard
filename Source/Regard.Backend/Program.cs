using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
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
                CreateHostBuilder(args).Build().Run();
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
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).Build();

            GlobalDiagnosticsContext.Set("DataDirectory", config["DataDirectory"]);

            return NLogBuilder.ConfigureNLog(LogManager.Configuration).GetCurrentClassLogger();
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
