namespace Ingress.Asb.Worker.Models
{
    public class RoadMeasureValue : IoTHubMessage
    {
        public int Humidity { get; }

        public RoadMeasureValue(IoTHubMessageOrigin origin, string timestamp, int humidity) : base(origin, timestamp, "telemetry.roadmeasurevalue")
        {
            Humidity = humidity;
        }
    }

}
