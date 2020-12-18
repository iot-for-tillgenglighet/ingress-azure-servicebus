

using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Ingress.Asb.Worker.Models
{
    public partial class RoadSegment
    {   
        [JsonProperty("id")]
        public string ID { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("location")]
        public GeoProperty Location { get; set; }

        [JsonProperty("surfaceType")]
        public SurfaceType SurfaceType { get; set;}

        public RoadSegment(string id, string value, double probability) {
            ID = id;
            Type = "RoadSegment";
            SurfaceType = new SurfaceType(value, probability);
        }

    }

    public class SurfaceType {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("probability")]
        public double Probability { get; set; }

        public SurfaceType(string value, double probability){

            Type = "Property";
            Value = value;
            Probability = probability;

        }
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
