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
    public class ServiceBusClientTests
    {
        string messageContents = "{ \"createdUTC\": \"2020-05-27T15:38:19.208757Z\", \"predictions\": [{\"probability\": 0.016200000420212746, \"tagName\": \"GRASS\"}, {\"probability\": 0.00032017999910749495, \"tagName\": \"GRAVEL\" }, {\"probability\": 0.79594802856445313, \"tagName\": \"SNOW\" }, { \"probability\": 0.18753175437450409, \"tagName\": \"TARMAC\" } ], \"position\": { \"status\": \"1\", \"accuracy\": \"0.700000\", \"latitude\": \"62.410672\", \"longitude\": \"17.270033\", \"angle\": \"38.700000\" } }";
        string roadSegments = "[{\"@context\":[\"https://schema.lab.fiware.org/ld/context\", \"https://uri.etsi.org/ngsi-ld/v1/ngsi-ld-core-context.jsonld\"], \"endPoint\":{\"type\":\"GeoProperty\", \"value\":{\"coordinates\":[17.269771,62.410616], \"type\":\"Point\"}}, \"id\":\"urn:ngsi-ld:RoadSegment:3:776896\", \"location\":{\"type\":\"GeoProperty\", \"value\":{\"coordinates\":[[17.270159,62.409858], [17.270083,62.409908], [17.270058,62.409926], [17.270029,62.409953], [17.269989,62.409995], [17.269967,62.410022], [17.269929,62.410091], [17.269914,62.410125], [17.269905,62.410151], [17.269898,62.410185], [17.269893,62.410237], [17.269894,62.410516], [17.269885,62.410544], [17.269861,62.410569], [17.269825,62.410592], [17.269781,62.410612], [17.269771,62.410616]], \"type\":\"LineString\"}}, \"name\":{\"type\":\"Property\", \"value\":\"3:776896\"}, \"refRoad\":{\"object\":\"urn:ngsi-ld:Road:3:776896\", \"type\":\"Relationship\"}, \"startPoint\":{\"type\":\"GeoProperty\", \"value\":{\"coordinates\":[17.270159,62.409858], \"type\":\"Point\"}}, \"totalLaneNumber\":{\"type\":\"Property\", \"value\": 1}, \"type\": \"RoadSegment\"}, {\"@context\":[\"https://schema.lab.fiware.org/ld/context\", \"https://uri.etsi.org/ngsi-ld/v1/ngsi-ld-core-context.jsonld\"], \"endPoint\":{\"type\":\"GeoProperty\", \"value\":{\"coordinates\":[17.270655,62.410113], \"type\":\"Point\"}}, \"id\":\"urn:ngsi-ld:RoadSegment:16172:578724\",\"location\":{\"type\":\"GeoProperty\", \"value\": {\"coordinates\": [[17.270655,62.410113], [17.270645,62.410656], [17.270574,62.410685], [17.270328,62.410685], [17.270246,62.410661], [17.27021,62.410628], [17.270251,62.410144], [17.270287,62.410115], [17.270384,62.410106], [17.270655,62.410113]],\"type\":\"LineString\"}}, \"name\": {\"type\":\"Property\", \"value\":\"16172:578724\"}, \"refRoad\":{\"object\":\"urn:ngsi-ld:Road:16172:578724\", \"type\":\"Relationship\"}, \"startPoint\": {\"type\":\"GeoProperty\", \"value\": {\"coordinates\": [17.270655,62.410113], \"type\": \"Point\"}}, \"totalLaneNumber\": {\"type\":\"Property\", \"value\": 1}, \"type\":\"RoadSegment\"}]";

        static System.Func<Message, CancellationToken, Task> messageHandlerFunc = null;

        [TestMethod]
        public async Task TestThatServiceBusClientHandlesIncomingMessageAsExpected()
        {
            // Arrange
            IServiceCollection services = CreateDIServiceCollection();
        
            Mock<HttpMessageHandler> mockHttpMessageHandler = CreateMockedHttpMessageHandler(
                HttpStatusCode.OK, roadSegments, HttpStatusCode.NoContent, ""
                );

            var httpMock = CreateMockedHttpClientFactory(mockHttpMessageHandler.Object);
            services.AddSingleton<IHttpClientFactory>(httpMock.Object);

            var hostedService = await StartHostedService(services);

            // Act
            await messageHandlerFunc(createMessageFromBody(messageContents), CancellationToken.None);

            await hostedService.StopAsync(CancellationToken.None);

            // Assert
            // TODO: Verify that Patch has been called with correct URL.
        }

        [TestMethod]
        public async Task TestThatServiceBusClientHandlesEmptyList()
        {
            // Arrange
            IServiceCollection services = CreateDIServiceCollection();
        
            Mock<HttpMessageHandler> mockHttpMessageHandler = CreateMockedHttpMessageHandler(HttpStatusCode.OK, "[]");

            var httpMock = CreateMockedHttpClientFactory(mockHttpMessageHandler.Object);
            services.AddSingleton<IHttpClientFactory>(httpMock.Object);

            var hostedService = await StartHostedService(services);
            
            // Act
            await messageHandlerFunc(createMessageFromBody(messageContents), CancellationToken.None);

            await hostedService.StopAsync(CancellationToken.None);

            // Assert
            // TODO: verify that patch has not been called.
        }

        [TestMethod]
        public async Task TestThatServiceBusClientHandlesFailedRequests()
        {
            // Arrange
            IServiceCollection services = CreateDIServiceCollection();
        
            Mock<HttpMessageHandler> mockHttpMessageHandler = CreateMockedHttpMessageHandler(HttpStatusCode.InternalServerError, "[]");

            var httpMock = CreateMockedHttpClientFactory(mockHttpMessageHandler.Object);
            services.AddSingleton<IHttpClientFactory>(httpMock.Object);
            
            var hostedService = await StartHostedService(services);

            // Act
            await messageHandlerFunc(createMessageFromBody(messageContents), CancellationToken.None);

            await hostedService.StopAsync(CancellationToken.None);

            // Assert
            // TODO: verify that patch has not been called.
        }

        [TestMethod]
        public async Task TestThatServiceBusClientHandlesBadPatchRequests()
        {
            // Arrange
            IServiceCollection services = CreateDIServiceCollection();
        
            Mock<HttpMessageHandler> mockHttpMessageHandler = CreateMockedHttpMessageHandler(
                HttpStatusCode.OK, roadSegments, HttpStatusCode.BadRequest, ""
                );
            
            var httpMock = CreateMockedHttpClientFactory(mockHttpMessageHandler.Object);
            services.AddSingleton<IHttpClientFactory>(httpMock.Object);
            
            var hostedService = await StartHostedService(services);

            // Act
            await messageHandlerFunc(createMessageFromBody(messageContents), CancellationToken.None);

            await hostedService.StopAsync(CancellationToken.None);

            // Assert
            // TODO: Verify that patch has been called
        }

        private static IServiceCollection CreateDIServiceCollection(){
            IServiceCollection services = new ServiceCollection();
            services.AddSingleton<IHostedService, ServiceBusClient>();
            services.AddLogging();

            var config = new ConfigurationBuilder().Build();
            services.AddSingleton<IConfiguration>(config);

            var asbClientMock = new Mock<ISubscriptionClient>();
            asbClientMock.Setup(m => m.RegisterMessageHandler(
                It.IsAny<System.Func<Message, CancellationToken, Task>>(),
                It.IsAny<MessageHandlerOptions>()))
                .Callback<System.Func<Message, CancellationToken, Task>, MessageHandlerOptions>((handler, opts) =>
                {
                    messageHandlerFunc = handler;
                });
            services.AddSingleton<ISubscriptionClient>(asbClientMock.Object);

            return services;
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

        private static Mock<IHttpClientFactory> CreateMockedHttpClientFactory(HttpMessageHandler httpMessageHandler) {
            var client = new HttpClient(httpMessageHandler);
            var httpMock = new Mock<IHttpClientFactory>();
            httpMock.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(client);
            return httpMock;
        }

        private static Mock<HttpMessageHandler> CreateMockedHttpMessageHandler(HttpStatusCode code1, string content1, HttpStatusCode code2 = HttpStatusCode.InternalServerError, string content2 = "") {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = code1,
                    Content = new StringContent(content1),
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = code2,
                    Content = new StringContent(content2),
                });

            return mockHttpMessageHandler;
        }

        private static async Task<IHostedService> StartHostedService(IServiceCollection services) {
            var serviceProvider = services.BuildServiceProvider();
            var hostedService = serviceProvider.GetService<IHostedService>();

            // Act
            await hostedService.StartAsync(CancellationToken.None);

            while (messageHandlerFunc == null) {
                await Task.Delay(100);
            }

            return hostedService;
        }
    }
}