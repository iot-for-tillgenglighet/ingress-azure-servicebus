namespace Ingress.Asb.Worker.Models
{
    public class RoadMeasureValue : IoTHubMessage
    {
        public string SurfaceType { get; }
        public string Status { get; }
        public string Accuracy { get; }
        public string Angle { get; }


        public RoadMeasureValue(IoTHubMessageOrigin origin, string timestamp, string surfaceType, string status, string accuracy, string angle) : base(origin, timestamp, "telemetry.roadmeasurevalue")
        {
            SurfaceType = surfaceType;
            Status = status;
            Accuracy = accuracy;
            Angle = angle;
        }
    }

}
