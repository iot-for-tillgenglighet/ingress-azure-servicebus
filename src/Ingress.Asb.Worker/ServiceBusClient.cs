using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

using Ingress.Asb.Worker.Models;

using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Ingress.Asb.Worker
{
    public class ServiceBusClient : BackgroundService
    {
        private readonly ILogger<ServiceBusClient> _logger;
        private readonly IConfiguration _configuration;
        private ISubscriptionClient _subscriptionClient;
        private readonly IHttpClientFactory _httpClientFactory;
        public ServiceBusClient(ILogger<ServiceBusClient> logger, IConfiguration configuration, ISubscriptionClient subscriptionClient, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _subscriptionClient = subscriptionClient;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"Starting servicebus client");
            RegisterOnMessageHandlerAndReceiveMessages();
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
            _logger.LogInformation($"Stopping servicebus client");
        }

        private void RegisterOnMessageHandlerAndReceiveMessages()
        {
            // Configure the message handler options in terms of exception handling, number of concurrent messages to deliver, etc.
            var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                // Maximum number of concurrent calls to the callback ProcessMessagesAsync(), set to 1 for simplicity.
                // Set it according to how many messages the application wants to process in parallel.
                MaxConcurrentCalls = 1,

                // Indicates whether the message pump should automatically complete the messages after returning from user callback.
                // False below indicates the complete operation is handled by the user callback as in ProcessMessagesAsync().
                AutoComplete = false
            };

            // Register the function that processes messages.
            _subscriptionClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);
        }

private async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            string json = Encoding.UTF8.GetString(message.Body);
            // Process the message.
            Console.WriteLine($"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{json}");

            RoadAvailabilityModel roadAvailabilityModel = JsonConvert.DeserializeObject<RoadAvailabilityModel>(json);

            double latitude = double.Parse(roadAvailabilityModel.Position.Latitude, System.Globalization.CultureInfo.InvariantCulture);
            double longitude = double.Parse(roadAvailabilityModel.Position.Longitude, System.Globalization.CultureInfo.InvariantCulture);

            //var baseUrl = Environment.GetEnvironmentVariable("BASE_URL");

            int distance = 30;

            string newUrl = $"https://iotsundsvall.se/ngsi-ld/v1/entities?type=RoadSegment&georel=near;maxDistance=={distance}&geometry=Point&coordinates=[{longitude},{latitude}]";

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(newUrl);
 
            if (response.IsSuccessStatusCode)
            {
                var result = response.Content.ReadAsStreamAsync().Result;
            }

            // Complete the message so that it is not received again.
            // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
            await _subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);

            // Note: Use the cancellationToken passed as necessary to determine if the subscriptionClient has already been closed.
            // If subscriptionClient has already been closed, you can choose to not call CompleteAsync() or AbandonAsync() etc.
            // to avoid unnecessary exceptions.
        }

        private IIoTHubMessage ConvertToIotHubMessage(Message message)
        {
            string json = Encoding.UTF8.GetString(message.Body);

            RoadAvailabilityModel roadAvailabilityModel = JsonConvert.DeserializeObject<RoadAvailabilityModel>(json);

            double latitude = double.Parse(roadAvailabilityModel.Position.Latitude, System.Globalization.CultureInfo.InvariantCulture);
            double longitude = double.Parse(roadAvailabilityModel.Position.Longitude, System.Globalization.CultureInfo.InvariantCulture);

            // Todo: Borde inte device ing� i JSON message?
            IoTHubMessageOrigin origin = new IoTHubMessageOrigin("device", latitude, longitude);
            // Todo: �r tidst�mpeln UTC eller lokaltid?
            RoadMeasureValue roadMeasureValue = new RoadMeasureValue(origin, roadAvailabilityModel.Created.ToString(), GetSurfaceType(roadAvailabilityModel), roadAvailabilityModel.Position.Status.ToString(), roadAvailabilityModel.Position.Accuracy, roadAvailabilityModel.Position.Angle);

            return roadMeasureValue;
        }

        private string GetSurfaceType(RoadAvailabilityModel roadAvailabilityModel)
        {
            var probability = roadAvailabilityModel.Predictions.OrderByDescending(x => x.Probability).First();
            return probability.TagName;
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _logger.LogInformation($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");

            // Todo: Error handling. Hur skall vi l�gga meddelandet p� deadletter k�n?

            return Task.CompletedTask;
        }
    }
}
