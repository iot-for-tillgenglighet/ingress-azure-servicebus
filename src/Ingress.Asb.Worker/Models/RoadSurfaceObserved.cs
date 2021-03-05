

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
        public GeoProperty Position { get; set; }

        [JsonProperty("surfaceType")]
        public SurfaceType SurfaceType { get; set; }

        [JsonProperty("timeObserved")]
        public DateTimeOffset TimeObserved { get; set; }

        [JsonProperty("refRoadSegment")]
        public string refRoadSegment { get; set; }

        public RoadSurfaceObserved(string id, string value, double probability, double[] latitude, double[] longitude) {
            ID = id;
            Type = "RoadSurfaceObserved";
            SurfaceType = new SurfaceType(value, probability);
            Position = new GeoProperty(latitude, longitude);
        }
    }
}
