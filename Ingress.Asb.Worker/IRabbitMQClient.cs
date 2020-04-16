using Ingress.Asb.Worker.Models;

namespace Ingress.Asb.Worker
{
    public interface IRabbitMQClient
    {
        void Initialize();
        void PostMessage(IIoTHubMessage message);
    }
}