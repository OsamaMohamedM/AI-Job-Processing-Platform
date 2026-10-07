using Infrastructure.Persistence.Messages;
using RabbitMQ.Client;

namespace Infrastructure.Persistence.Messaging;

public static class RabbitMqTopology
{
    public static async Task DeclareAsync(IChannel ch, RabbitMqOptions o, CancellationToken ct)
    {
        await ch.ExchangeDeclareAsync(o.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await ch.QueueDeclareAsync(o.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);
        await ch.QueueBindAsync(o.DeadLetterQueue, o.DeadLetterExchange, o.DeadLetterRoutingKey, cancellationToken: ct);

        await ch.ExchangeDeclareAsync(o.Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        var args = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = o.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = o.DeadLetterRoutingKey
        };
        await ch.QueueDeclareAsync(o.Queue, durable: true, exclusive: false, autoDelete: false, arguments: args, cancellationToken: ct);
        await ch.QueueBindAsync(o.Queue, o.Exchange, o.RoutingKey, cancellationToken: ct);
    }
}