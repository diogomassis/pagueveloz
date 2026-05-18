using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PagueVeloz.Application.Events;
using PagueVeloz.Infrastructure.Messaging;
using RabbitMQ.Client;
using Xunit;

namespace PagueVeloz.Tests.Integration;

public class RabbitMqPublisherIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RabbitMqEventPublisher_publishes_message_to_exchange()
    {
        var host = Environment.GetEnvironmentVariable("TEST_RABBIT_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("TEST_RABBIT_PORT") ?? "5672";
        var user = Environment.GetEnvironmentVariable("TEST_RABBIT_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("TEST_RABBIT_PASS") ?? "guest";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string,string>("Messaging:RabbitMq:Host", host),
                new KeyValuePair<string,string>("Messaging:RabbitMq:Port", portStr),
                new KeyValuePair<string,string>("Messaging:RabbitMq:Username", user),
                new KeyValuePair<string,string>("Messaging:RabbitMq:Password", pass),
                new KeyValuePair<string,string>("Messaging:RabbitMq:Exchange", "pagueveloz.transactions")
            })
            .Build();
        // create a consumer queue and bind to the exchange
        var factory = new ConnectionFactory { HostName = host, Port = int.Parse(portStr), UserName = user, Password = pass };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        var exchange = "pagueveloz.transactions";
        channel.ExchangeDeclare(exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        var queue = channel.QueueDeclare(queue: string.Empty, durable: false, exclusive: true, autoDelete: true);
        channel.QueueBind(queue.QueueName, exchange, "transaction.processed");
        // create publisher
        var publisher = new RabbitMqEventPublisher(configuration, new NullLogger<RabbitMqEventPublisher>());
        var @event = new TransactionProcessedEvent(
            TransactionId: Guid.NewGuid().ToString(),
            Operation: "credit",
            AccountId: "acc-1",
            TargetAccountId: null,
            Amount: 500,
            Status: "success",
            Timestamp: DateTimeOffset.UtcNow
        );
        await publisher.PublishAsync(@event);
        // try to fetch the message from the queue
        var attempts = 0;
        BasicGetResult? result = null;
        while (attempts < 10)
        {
            result = channel.BasicGet(queue.QueueName, autoAck: true);
            if (result != null) break;
            await Task.Delay(200);
            attempts++;
        }
        Assert.NotNull(result);
        var body = Encoding.UTF8.GetString(result.Body.ToArray());
        var received = JsonSerializer.Deserialize<TransactionProcessedEvent>(body);
        Assert.NotNull(received);
        Assert.Equal(@event.TransactionId, received!.TransactionId);
        Assert.Equal(@event.Operation, received.Operation);
    }
}
