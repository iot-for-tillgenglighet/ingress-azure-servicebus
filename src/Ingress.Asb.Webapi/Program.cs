using Ingress.Asb.Worker;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;

using Serilog;
using Serilog.Formatting.Compact;

namespace Ingress.Asb.Webapi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .CreateLogger();

            try {
                Log.Information("Starting up ingress-azure-servicebus.");
                CreateHostBuilder(args).Build().Run();
            }
            catch (ArgumentException ae) {
                Log.Fatal($"Failed to start due to an illegal argument: {ae.Message}");
            }
            catch (Exception ex) {
                Log.Fatal(ex, "Failed to start due to an unknown exception.");
            }
            finally {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddEnvironmentVariables(prefix: "DIWISE_");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<ServiceBusClient>();
                    services.AddHttpClient();
                });
    }
}
