using System;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ingress_asb_client
{
    class App
    {
        private readonly IConfiguration _config;
        private readonly ILogger _logger;

        public App(IConfiguration config, ILogger<App> logger)
        {
            _config = config;
            _logger = logger;
        }

        public void Run()
        {
            _logger.LogInformation("logging information");

            Console.WriteLine("Hello from App.cs");
        }

    }
}
