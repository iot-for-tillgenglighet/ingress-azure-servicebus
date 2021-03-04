

using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Ingress.Asb.Worker.Models
{
    public partial class RoadSurfaceObserved
    {   
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("position")]
        public RoadPosition Position { get; set; }

        [JsonProperty("surfaceType")]
        public SurfaceType SurfaceType { get; set; }

        [JsonProperty("timeObserved")]
        public DateTimeOffset TimeObserved { get; set; }

        [JsonProperty("refRoadSegmentID")]
        public string refRoadSegmentID { get; set; }

        public RoadSurfaceObserved(string id, string value, double probability, double latitude, double longitude) {
            ID = id;
            Type = "RoadSurfaceObserved";
            SurfaceType = new SurfaceType(value, probability);
            Position = new RoadPosition(latitude, longitude);
        }
    }

    public class RoadPosition
    {
        [JsonProperty("latitude")]
        public double Latitude { get; set; }

        [JsonProperty("longitude")]
        public double Longitude { get; set; }
        
        public RoadPosition(double latitude, double longitude) {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
