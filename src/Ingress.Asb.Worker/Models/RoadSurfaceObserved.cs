

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

        [JsonProperty("location")]
        public Location Position { get; set; }

        [JsonProperty("surfaceType")]
        public SurfaceType SurfaceType { get; set; }

        [JsonProperty("refRoadSegment")]
        public string refRoadSegment { get; set; }

        public RoadSurfaceObserved(string id, string value, double probability, double latitude, double longitude) {
            ID = id;
            Type = "RoadSurfaceObserved";
            SurfaceType = new SurfaceType(value, probability);
            Position = new Location(latitude, longitude);
        }
    }

public class Location
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public LocationValue Value { get; set; }

        public Location(double latitude, double longitude)
        {
            Type = "GeoProperty";
            Value = new LocationValue(latitude, longitude);
        }
    }
    
    public class LocationValue
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("coordinates")]
        public double[] Coordinates { get; set; }

        public LocationValue(double latitude, double longitude)
        {
            Type = "Point";
            Coordinates = new double[] { longitude, latitude };
        }
    }
}
