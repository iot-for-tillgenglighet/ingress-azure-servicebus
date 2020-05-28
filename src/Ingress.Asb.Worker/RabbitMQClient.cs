using System;
using System.Text;

using Ingress.Asb.Worker.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using RabbitMQ.Client;

namespace Ingress.Asb.Worker
{
    public class RabbitMQClient : IRabbitMQClient
    {
        private IConnection _rmqConnection;
        private IModel _rmqModel;
        private const string _exchangeName = "iot-msg-exchange-topic";
        private JsonSerializerSettings _serializerSettings;
        private bool _debugEnvironment = false;

        public RabbitMQClient()
        {
            Initialize();
        }

        public void Initialize()
        {

            try
            {
                ConnectionFactory rmqFactory = new ConnectionFactory
                {
                    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST"),
                    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER"),
                    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
                };

                if (rmqFactory.HostName != null)
                {
                    Console.WriteLine($"Connecting to RabbitMQ host {rmqFactory.HostName} as {rmqFactory.UserName} ...");
                    _rmqConnection = rmqFactory.CreateConnection();
                    _rmqModel = _rmqConnection.CreateModel();
                    _rmqModel.ExchangeDeclare(_exchangeName, ExchangeType.Topic);
                    _serializerSettings = new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        NullValueHandling = NullValueHandling.Ignore
                    };
                }
                else
                {
                    _debugEnvironment = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"RabbitMQ Exception: {e.Message}");
                if (_debugEnvironment == false)
                {
                    System.Environment.Exit(1);
                }
            }
        }


        public void PostMessage(IIoTHubMessage message)
        {
            if (message != null && !_debugEnvironment)
            {
                string json = JsonConvert.SerializeObject(message, _serializerSettings);
                byte[] messageBodyBytes = Encoding.UTF8.GetBytes(json);

                _rmqModel.BasicPublish(_exchangeName, message.Topic, null, messageBodyBytes);
            }
        }
    }
}
