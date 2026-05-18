using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Abstractions;
using PagueVeloz.Application.Events;
using RabbitMQ.Client;

namespace PagueVeloz.Infrastructure.Messaging;

public sealed class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private IConnection? _connection;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly string _exchangeName;
    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        _exchangeName = configuration["Messaging:RabbitMq:Exchange"] ?? "pagueveloz.transactions";

        var factory = new ConnectionFactory
        {
            HostName = configuration["Messaging:RabbitMq:Host"] ?? "localhost",
            Port = int.TryParse(configuration["Messaging:RabbitMq:Port"], out var port) ? port : 5672,
            UserName = configuration["Messaging:RabbitMq:Username"] ?? "guest",
            Password = configuration["Messaging:RabbitMq:Password"] ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
        _factory = factory;
    }

    public Task PublishAsync(TransactionProcessedEvent @event, CancellationToken cancellationToken = default)
    {
        // ensure connection is ready (lazy connect)
        EnsureConnectedAsync(cancellationToken).GetAwaiter().GetResult();
        // retry with exponential backoff and jitter. Configuration keys (optional):
        // essaging:RabbitMq:PublishMaxAttempts (int, default 5)
        // Messaging:RabbitMq:PublishBaseDelayMs (int, default 200)
        var maxAttempts = int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_PUBLISH_MAX_ATTEMPTS"), out var e) && e > 0 ? e : 5;
        var baseDelayMs = int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_PUBLISH_BASE_DELAY_MS"), out var bd) && bd > 0
            ? bd
            : 200;
        var rnd = new Random();
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var channel = _connection!.CreateModel();
                channel.ConfirmSelect();
                channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
                var payload = JsonSerializer.SerializeToUtf8Bytes(@event);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;
                properties.ContentType = "application/json";
                channel.BasicPublish(
                    exchange: _exchangeName,
                    routingKey: "transaction.processed",
                    mandatory: false,
                    basicProperties: properties,
                    body: payload);
                channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
                return Task.CompletedTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Publish cancelled via cancellation token.");
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Publish attempt {Attempt} failed.", attempt);
                if (attempt == maxAttempts)
                {
                    _logger.LogError(exception, "Failed to publish transaction event after {Attempts} attempts.", maxAttempts);
                    break;
                }
                var backoff = (int)(baseDelayMs * Math.Pow(2, attempt - 1));
                var jitter = rnd.Next(0, Math.Min(1000, backoff));
                var delayMs = backoff + jitter;
                try
                {
                    Task.Delay(delayMs, cancellationToken).Wait(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Publish backoff cancelled.");
                    break;
                }
            }
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            _connection?.Close();
            _connection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while disposing RabbitMQ connection");
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true }) return;

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { IsOpen: true }) return;

            var connectMaxAttempts = int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_RABBITMQ_CONNECT_MAX_ATTEMPTS"), out var cma) && cma > 0 ? cma : 10;
            var connectBaseDelayMs = int.TryParse(Environment.GetEnvironmentVariable("PAGUEVELOZ_RABBITMQ_CONNECT_BASE_DELAY_MS"), out var cbd) && cbd > 0 ? cbd : 200;
            var rnd = new Random();

            for (var attempt = 1; attempt <= connectMaxAttempts; attempt++)
            {
                try
                {
                    _connection = _factory.CreateConnection();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RabbitMQ connect attempt {Attempt} failed.", attempt);
                    if (attempt == connectMaxAttempts)
                    {
                        _logger.LogError(ex, "Failed to connect to RabbitMQ after {Attempts} attempts.", connectMaxAttempts);
                        throw;
                    }

                    var backoff = (int)(connectBaseDelayMs * Math.Pow(2, attempt - 1));
                    var jitter = rnd.Next(0, Math.Min(1000, backoff));
                    var delayMs = backoff + jitter;

                    try
                    {
                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Connect backoff cancelled");
                        throw;
                    }
                }
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }
}