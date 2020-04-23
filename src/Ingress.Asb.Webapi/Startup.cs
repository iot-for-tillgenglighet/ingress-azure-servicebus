using System;

using Ingress.Asb.Worker;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ingress.Asb.Webapi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            //string connectionString = Configuration["Connectionstring"];
            //string topicPath = Configuration["TopicName"];
            //string subscriptionName = Configuration["SubscriptionName"];

            string connectionString = Environment.GetEnvironmentVariable("Connectionstring");
            string topicPath = Environment.GetEnvironmentVariable("TopicName");
            string subscriptionName = Environment.GetEnvironmentVariable("SubscriptionName");

            services.AddSingleton((ISubscriptionClient)new SubscriptionClient(connectionString, topicPath, subscriptionName));
            services.AddSingleton<IRabbitMQClient, RabbitMQClient>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
