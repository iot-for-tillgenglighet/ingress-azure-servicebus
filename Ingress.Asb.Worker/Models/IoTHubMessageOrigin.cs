namespace Ingress.Asb.Worker.Models
{
    public class IoTHubMessageOrigin
    {
        public string Device { get; }
        public double? Latitude { get; }
        public double? Longitude { get; }

        public IoTHubMessageOrigin(string device)
        {
            Device = device;
        }

        public IoTHubMessageOrigin(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public IoTHubMessageOrigin(string device, double latitude, double longitude)
        {
            Device = device;
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
