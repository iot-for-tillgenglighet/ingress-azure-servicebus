using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;

using Ingress.Asb.Worker.Models;

using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
            // Process the message.
            string messageJson = Encoding.UTF8.GetString(message.Body);
            _logger.LogInformation($"Received message: SequenceNumber: {message.SystemProperties.SequenceNumber} Body:{messageJson}");

            RoadAvailabilityModel roadAvailabilityModel = JsonConvert.DeserializeObject<RoadAvailabilityModel>(messageJson);

            double latitude = roadAvailabilityModel.Position.Latitude;
            double longitude = roadAvailabilityModel.Position.Longitude;
            int distance = 30;

            // Get all road segments within 30m of the location taken from message body.
            string newUrl = $"https://iotsundsvall.se/ngsi-ld/v1/entities?type=RoadSegment&georel=near;maxDistance=={distance}&geometry=Point&coordinates=[{longitude},{latitude}]";
            
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(newUrl);

            //Create new list to store the roadSegments we get in our response, if the Get is successful.
            var nearbyRoadSegments = new List<RoadSegment>();
 
            if (response.IsSuccessStatusCode)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                nearbyRoadSegments = JsonConvert.DeserializeObject<List<RoadSegment>>(result);

                //Get the surfaceType and Probability from the message.Body
                var prediction = roadAvailabilityModel.Predictions.OrderByDescending(x => x.Probability).First();
                var surfaceType = Convert.ToString(prediction.TagName).ToLower();
            
                //Patch RoadSegments, provided that our list is not empty.
                if (nearbyRoadSegments.Count > 0) {
                    int closestSegmentIndex = 0;

                    if (nearbyRoadSegments.Count > 1) {
                        // Find the nearest roadSegment to the location in message. 
                        double closestSegmentDistance = DistanceToSegmentFromLocation(nearbyRoadSegments[0], latitude, longitude);
                        for (int i = 1; i < nearbyRoadSegments.Count; i++) {
                            double segmentDistance = DistanceToSegmentFromLocation(nearbyRoadSegments[i], latitude, longitude);

                            if (segmentDistance < closestSegmentDistance) {
                                closestSegmentDistance = segmentDistance;
                                closestSegmentIndex = i;
                                Console.WriteLine($"The closest road Segment is {nearbyRoadSegments[closestSegmentIndex].ID}, with a distance of {closestSegmentDistance} meters.");
                            }
                        }
                    }

                    RoadSegment roadSegment = nearbyRoadSegments[closestSegmentIndex];
                    string roadSegID = roadSegment.ID;
                    string patchURL = $"https://iotsundsvall.se/ngsi-ld/v1/entities/{roadSegID}/attrs/";

                    var roadSegmentPatch = new RoadSegment(roadSegID, surfaceType, prediction.Probability);

                    var settings = new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    };

                    var roadSegJson = JsonConvert.SerializeObject(roadSegmentPatch, settings);
                    var data = new StringContent(roadSegJson, Encoding.UTF8, "application/ld+json");

                    var patchResponse = await client.PatchAsync(patchURL, data);

                    if (patchResponse.IsSuccessStatusCode) {
                        var stringContent = await patchResponse.Content.ReadAsStringAsync();
                        Console.WriteLine(patchResponse.StatusCode + ": " + stringContent);
                    } else {
                        _logger.LogError($"Failed to patch road segment {roadSegID}: {patchResponse.StatusCode}. Content: {patchResponse.Content}.");
                    }

                } else {
                    _logger.LogWarning($"No road segments found near {latitude}, {longitude}.");
                }
                
                // Complete the message so that it is not received again.
                // This can be done only if the subscriptionClient is created in ReceiveMode.PeekLock mode (which is the default).
                await _subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
            } else {
                _logger.LogWarning($"Failed to retrieve road segments: {response.StatusCode}. Content: {response.Content}." );
            }
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _logger.LogInformation($"Message handler encountered an exception: {exceptionReceivedEventArgs.Exception.Message}.");

            // TODO: Error handling. Hur skall vi l�gga meddelandet p� deadletter k�n?

            return Task.CompletedTask;
        }

        private static int DistanceToSegmentFromLocation(RoadSegment roadSegment, double latitude, double longitude) {
            
            // TODO: Calculate the distance.
            List<int> distanceToLine = new List<int>();
            // iterate over roadSegment coordinates
            var roadSegLatLong = roadSegment.Location.Value.Coordinates; 
            for (int i = 1; i < roadSegLatLong.Length; i++) {
                // Create lines between coordinate pairs - between coordinate 0-1, 1-2, 2-3, 2-3
                double[][] line = {roadSegLatLong[i - 1], roadSegLatLong[i]};
                
                distanceToLine.Add(DistanceToLineFromLocation(line[0][0], line[0][1], line[1][0], line[1][1], latitude, longitude));
            }

            // distance to closest line is distance to roadsegment
            int distanceToSegment = distanceToLine.Min();
            Console.WriteLine($"The closest roadSegment line is {distanceToSegment} meters away.");

            //return distanceToSegment
            return distanceToSegment;
        }

        private static int DistanceToLineFromLocation(double lineLat0, double lineLon0, double lineLat1, double lineLon1, double latitude, double longitude) {
                // find closest point on road segment line to the location from our message body
                double[] pointAtoP = {lineLat0 - latitude, lineLon0 - longitude};
                double[] pointAtoB = {lineLat1 - latitude, lineLon1 - longitude};

                double fromAtoB = Math.Pow(pointAtoB[0],2) + Math.Pow(pointAtoB[1],2);
                double pointATimesPointB = pointAtoP[0] * pointAtoB[0] + pointAtoP[1] * pointAtoB[1];

                double distanceFromAToClosestPoint = pointATimesPointB / fromAtoB;
                
                double[] closestPoint = {latitude + pointAtoB[0] * distanceFromAToClosestPoint, longitude + pointAtoB[1] * distanceFromAToClosestPoint};
        
                double distance = ConvertDistanceBetweenTwoPointsToMeters(closestPoint, latitude, longitude);
                Console.WriteLine($"The distance in meters between the nearest point of the roadSegment Line and the message location is: {distance}");

            return Convert.ToInt32(distance);
        }

        private static int ConvertDistanceBetweenTwoPointsToMeters(double[] closestPoint, double latitude, double longitude) {
            // Haversine formula 
            double earthRadius = 6378.137;
            double lat = closestPoint[1] * Math.PI / 180 - latitude * Math.PI / 180;
            double lon = closestPoint[0] * Math.PI / 180 - longitude * Math.PI / 180;
            double a =  Math.Sin(lat/2) * Math.Sin(lat/2) + 
                        Math.Cos(latitude * Math.PI / 180) * Math.Cos(closestPoint[1] * Math.PI / 180) *
                        Math.Sin(lon/2) * Math.Sin(lon/2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
            double d = earthRadius * c;

            int distanceInMeters = Convert.ToInt32(d * 1000);

            return distanceInMeters;
        }
    }
}
