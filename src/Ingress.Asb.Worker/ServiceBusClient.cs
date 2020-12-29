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
        private readonly ISubscriptionClient _subscriptionClient;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly string _contextBrokerURL;
        private readonly UInt32 _maxRoadSegmentDistance;

        public ServiceBusClient(ILogger<ServiceBusClient> logger, IConfiguration configuration, ISubscriptionClient subscriptionClient, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _subscriptionClient = subscriptionClient;
            _httpClientFactory = httpClientFactory;

            _contextBrokerURL = ReturnValidURLOrThrow(_configuration["CONTEXT_BROKER_URL"]);
            _maxRoadSegmentDistance = ReturnValidSegmentDistanceOrThrow(_configuration["MAX_SEGMENT_DISTANCE"]);
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

            double latitude = Convert.ToDouble(roadAvailabilityModel.Position.Latitude);
            double longitude = Convert.ToDouble(roadAvailabilityModel.Position.Longitude);

            // Get all road segments within a certain distance of the location taken from message body.
            string newUrl = $"{_contextBrokerURL}/ngsi-ld/v1/entities?type=RoadSegment&georel=near;maxDistance=={_maxRoadSegmentDistance}&geometry=Point&coordinates=[{longitude},{latitude}]";
            
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(newUrl);

            // Create new list to store the roadSegments we get in our response, if the Get is successful.
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
                        double closestSegmentDistance = DistanceToSegmentFromLocation(nearbyRoadSegments[0], longitude, latitude);
                        for (int i = 1; i < nearbyRoadSegments.Count; i++) {
                            double segmentDistance = DistanceToSegmentFromLocation(nearbyRoadSegments[i], longitude, latitude);

                            if (segmentDistance < closestSegmentDistance) {
                                closestSegmentDistance = segmentDistance;
                                closestSegmentIndex = i;
                            }
                        }
                        _logger.LogInformation($"The closest road Segment is {nearbyRoadSegments[closestSegmentIndex].ID}, with a distance of {closestSegmentDistance} meters.");
                    }

                    RoadSegment roadSegment = nearbyRoadSegments[closestSegmentIndex];
                    string roadSegID = roadSegment.ID;
                    string patchURL = $"{_contextBrokerURL}/ngsi-ld/v1/entities/{roadSegID}/attrs/";

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
                        _logger.LogInformation(patchResponse.StatusCode + ": " + stringContent);
                    } else {
                        _logger.LogError($"Failed to patch road segment {roadSegID}: {patchResponse.StatusCode}. Content: {patchResponse.Content}.");
                    }

                } else {
                    _logger.LogWarning($"No road segments found near {longitude}, {latitude}.");
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

            return Task.CompletedTask;
        }

        private int DistanceToSegmentFromLocation(RoadSegment roadSegment, double longitude, double latitude) {
            
            List<int> distanceToLine = new List<int>();
            // iterate over roadSegment coordinates
            var roadSegLatLong = roadSegment.Location.Value.Coordinates; 
            for (int i = 1; i < roadSegLatLong.Length; i++) {
                // Create lines between coordinate pairs - between coordinate 0-1, 1-2, 2-3, 2-3
                double[] startPoint = roadSegLatLong[i - 1];
                double[] endPoint = roadSegLatLong[i];
                
                distanceToLine.Add(DistanceToLineFromLocation(startPoint[0], startPoint[1], endPoint[0], endPoint[1], longitude, latitude));
            }

            // distance to closest line is distance to roadsegment
            int distanceToSegment = distanceToLine.Min();
            _logger.LogInformation($"The closest roadSegment line is {distanceToSegment} meters away.");

            return distanceToSegment;
        }

        private int DistanceToLineFromLocation(double startPointLon, double startPointLat, double endPointLon, double endPointLat, double longitude, double latitude) {
            // Find distance between startPoint and endPoint of line, find closest point on line to the message location.
            // Index 0 is always longitude, index 1 is always latitude.

            double[] fromStartPointToLocation = {startPointLon - longitude, startPointLat - latitude};
            double[] fromEndPointToLocation = {endPointLon - longitude, endPointLat - latitude};

            double fromStartPointToEndPoint = Math.Pow(fromEndPointToLocation[0],2) + Math.Pow(fromEndPointToLocation[1],2);
            
            double startPointToLocationTimesEndPointToLocation = fromStartPointToLocation[0] * fromEndPointToLocation[0] + fromStartPointToLocation[1] * fromEndPointToLocation[1];
            
            double distanceFromLocationToClosestPoint = startPointToLocationTimesEndPointToLocation / fromStartPointToEndPoint;
            
            // Get the coordinates of the closest point.
            double[] closestPoint = {longitude + fromEndPointToLocation[0] * distanceFromLocationToClosestPoint, latitude + fromEndPointToLocation[1] * distanceFromLocationToClosestPoint};
    
            double distance = ConvertDistanceBetweenTwoPointsToMeters(closestPoint, longitude, latitude);
            _logger.LogDebug($"The distance in meters between the nearest point of the roadSegment Line and the message location is: {distance}");

            return Convert.ToInt32(distance);
        }

        private static int ConvertDistanceBetweenTwoPointsToMeters(double[] closestPoint, double longitude, double latitude) {
            // Haversine formula 
            const double EarthRadius = 6378.137;
            double lon = closestPoint[0] * Math.PI / 180 - longitude * Math.PI / 180;
            double lat = closestPoint[1] * Math.PI / 180 - latitude * Math.PI / 180;
            double a =  Math.Sin(lat/2) * Math.Sin(lat/2) + 
                        Math.Cos(longitude * Math.PI / 180) * Math.Cos(closestPoint[0] * Math.PI / 180) *
                        Math.Sin(lon/2) * Math.Sin(lon/2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
            double d = EarthRadius * c;

            int distanceInMeters = Convert.ToInt32(d * 1000);

            return distanceInMeters;
        }

        private UInt32 ReturnValidSegmentDistanceOrThrow(string distanceAsString) {
            const UInt32 MaxSegmentDistance = 100;
            const UInt32 MinRecommendedSegmentDistance = 15;
            const UInt32 MinSegmentDistance = 5;

            UInt32 distance = 30;

            try
            {
                if (distanceAsString != null && distanceAsString.Length > 0)
                {
                    distance = System.UInt32.Parse(distanceAsString);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Max road segment distance could not be parsed from {distanceAsString}: {e.Message}");
            }

            if (distance > MaxSegmentDistance)
            {
                throw new ArgumentException($"Max road segment distance {distance}m is larger than the maximum allowed {MaxSegmentDistance}m!");
            }

            if (distance < MinSegmentDistance)
            {
                throw new ArgumentException($"Max road segment distance {distance}m is lower than the minimum allowed {MinSegmentDistance}m!");
            }
            else if (distance < MinRecommendedSegmentDistance)
            {
                _logger.LogWarning($"Max road segment distance {distance}m is lower than the recommended minimum {MinRecommendedSegmentDistance}m");
            }

            return distance;
        }

        private string ReturnValidURLOrThrow(string url) {
            Uri uri;

            try {
                uri = new Uri(url, UriKind.Absolute);
            } catch (Exception e) {
                throw new ArgumentException($"Failed to create a valid URI from context broker URL {url}: {e.Message}");
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) {
                throw new ArgumentException("The context broker URL must specify HTTP or HTTPS to be valid.");
            }

            return url;
        }
    }
}
