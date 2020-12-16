using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.Azure.ServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;

using Ingress.Asb.Worker;

using Moq;
using Moq.Protected;

namespace Ingress.Asb.Test
{
    [TestClass]
    public class UnitTest1
    {
        string messageContents = "{ \"createdUTC\": \"2020-05-27T15:38:19.208757Z\", \"predictions\": [{\"probability\": 0.016200000420212746, \"tagName\": \"GRASS\"}, {\"probability\": 0.00032017999910749495, \"tagName\": \"GRAVEL\" }, {\"probability\": 0.79594802856445313, \"tagName\": \"SNOW\" }, { \"probability\": 0.18753175437450409, \"tagName\": \"TARMAC\" } ], \"position\": { \"status\": \"1\", \"accuracy\": \"0.700000\", \"latitude\": \"62.410672\", \"longitude\": \"17.270033\", \"angle\": \"38.700000\" } }";
        
        [TestMethod]
        public async Task TestMethod1()
        {
            // Arrange
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IHostedService, ServiceBusClient>();
            services.AddLogging();

            var config = new ConfigurationBuilder().Build();
            services.AddSingleton<IConfiguration>(config);

            System.Func<Message, CancellationToken, Task> messageHandlerFunc = null;

            var asbClientMock = new Mock<ISubscriptionClient>();
            asbClientMock.Setup(m => m.RegisterMessageHandler(
                It.IsAny<System.Func<Message, CancellationToken, Task>>(),
                It.IsAny<MessageHandlerOptions>()))
                .Callback<System.Func<Message, CancellationToken, Task>, MessageHandlerOptions>((handler, opts) =>
                {
                    messageHandlerFunc = handler;
                });
            services.AddSingleton<ISubscriptionClient>(asbClientMock.Object);

            var httpMock = new Mock<IHttpClientFactory>();
            
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{'name':thecodebuzz,'city':'USA'}"),
                });

            var client = new HttpClient(mockHttpMessageHandler.Object);
            httpMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(client);

            services.AddSingleton<IHttpClientFactory>(httpMock.Object);

            var serviceProvider = services.BuildServiceProvider();
            var hostedService = serviceProvider.GetService<IHostedService>();

            // Act
            await hostedService.StartAsync(CancellationToken.None);

            while (messageHandlerFunc == null) {
                await Task.Delay(100);
            }

            await messageHandlerFunc(createMessageFromBody(messageContents), CancellationToken.None);

            await hostedService.StopAsync(CancellationToken.None);

            // Assert

        }

        private static Message createMessageFromBody(string body) {
            var message = new Message(Encoding.UTF8.GetBytes(body));

            var systemProperties = message.SystemProperties;
            var type = systemProperties.GetType();
            var lockToken = Guid.NewGuid();
            type.GetMethod("set_LockTokenGuid", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(systemProperties, new object[] { lockToken });
            type.GetMethod("set_SequenceNumber", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(systemProperties, new object[] { 0 });

            return message;
        }
    }
}