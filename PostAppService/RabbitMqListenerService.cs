using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace PostService
{
    public class RabbitMqListenerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqListenerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.Run(() => ListenForIntegrationEvents(stoppingToken), stoppingToken);
        }

        private void ListenForIntegrationEvents(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory() { HostName = "localhost" }; // Sesuaikan dengan host RabbitMQ
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: "user.postservice",
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            channel.QueueBind(queue: "user.postservice",
                              exchange: "user",
                              routingKey: "user.add");

            channel.QueueBind(queue: "user.postservice",
                              exchange: "user",
                              routingKey: "user.update");

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine(" [x] Received {0}", message);

                var data = JObject.Parse(message);
                var type = ea.RoutingKey;

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<PostServiceContext>();

                if (type == "user.add")
                {
                    Console.WriteLine("Processing user.add event...");
                    if (!dbContext.User.Any(a => a.ID == data["id"].Value<int>()))
                    {
                        dbContext.User.Add(new User()
                        {
                            ID = data["id"].Value<int>(),
                            Name = data["name"].Value<string>(),
                            Version = data["version"].Value<int>()
                        });
                        dbContext.SaveChanges();
                        Console.WriteLine("User added to PostService database.");
                    }
                }
                else if (type == "user.update")
                {
                    Console.WriteLine("Processing user.update event...");
                    int newVersion = data["version"].Value<int>();
                    var user = dbContext.User.FirstOrDefault(a => a.ID == data["id"].Value<int>());
                    if (user != null && user.Version < newVersion)
                    {
                        user.Name = data["newname"].Value<string>();
                        user.Version = newVersion;
                        dbContext.SaveChanges();
                        Console.WriteLine("User updated in PostService database.");
                    }
                }

                channel.BasicAck(ea.DeliveryTag, false);
            };

            channel.BasicConsume(queue: "user.postservice", autoAck: false, consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Keep the connection alive
                Thread.Sleep(1000);
            }
        }
    }
}
