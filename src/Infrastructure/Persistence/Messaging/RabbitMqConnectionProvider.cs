using Infrastructure.Persistence.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.Persistence.Messaging;

public sealed class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionProvider> log)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public async Task<IChannel> CreateChannelAsync(CreateChannelOptions channelOptions, CancellationToken ct)
    {
        var connection = await GetConnectionAsync(ct);
        return await connection.CreateChannelAsync(channelOptions, ct);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is not null) return _connection;
        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is not null) return _connection;
            var o = options.Value;
            var factory = new ConnectionFactory
            {
                HostName = o.Host,
                Port = o.Port,
                VirtualHost = o.VirtualHost,
                UserName = o.Username,
                Password = o.Password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };
            var c = await factory.CreateConnectionAsync("aijobs-worker", ct);
            c.ConnectionShutdownAsync += (_, e) => { log.LogWarning("RabbitMqConnectionLost {Reason}", e.ReplyText); return Task.CompletedTask; };
            c.RecoverySucceededAsync += (_, _) => { log.LogInformation("RabbitMqConnectionRecovered"); return Task.CompletedTask; };
            log.LogInformation("RabbitMqConnected {Host}", o.Host);
            return _connection = c;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) { await _connection.CloseAsync(); _connection.Dispose(); }
    }
}