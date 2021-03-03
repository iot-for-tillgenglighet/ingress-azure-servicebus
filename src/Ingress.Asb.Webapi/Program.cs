using Ingress.Asb.Worker;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.Net.Http;

using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Ingress.Asb.Webapi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
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
                    services.AddHttpClient("HttpClientSSLUntrusted", client => {
                    // code to configure headers etc..
                    }).ConfigurePrimaryHttpMessageHandler(() => {
                        var handler = new HttpClientHandler();
                        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; };
                        return handler;
                    });
                });

        
    }
}
