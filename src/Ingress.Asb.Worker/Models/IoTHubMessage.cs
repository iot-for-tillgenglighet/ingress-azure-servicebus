using System.Text.Json.Serialization;

namespace Ingress.Asb.Worker.Models
{
    public class IoTHubMessage : IIoTHubMessage
    {
        [JsonIgnore]
        public string Topic { get; }

        public IoTHubMessageOrigin Origin { get; }
        public string Timestamp { get; }

        public IoTHubMessage(IoTHubMessageOrigin origin, string timestamp, string topic)
        {
            Origin = origin;
            Timestamp = timestamp;
            Topic = topic;
        }
    }

    public interface IIoTHubMessage
    {
        string Topic { get; }
    }

}
