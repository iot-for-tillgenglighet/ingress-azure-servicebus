

using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Ingress.Asb.Worker
{
    public partial class RoadSegment
    {   
        [JsonProperty("context")]
        public Uri[] Context { get; set; }

        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("location")]
        public GeoProperty Location { get; set; }

    }

        public class GeoProperty
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public GeoPropertyValue Value { get; set; }

        public GeoProperty(double[] latitude, double[] longitude)
        {
            Type = "GeoProperty";
            Value = new GeoPropertyValue(latitude, longitude);
        }
    }

    public class GeoPropertyValue
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("coordinates")]
        public double[][] Coordinates { get; set; }

        public GeoPropertyValue(double[] latitude, double[] longitude)
        {
            Type = "Point";
            Coordinates = new double[][] { latitude, longitude };
        }

    }

}
