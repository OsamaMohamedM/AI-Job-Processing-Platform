using Infrastructure.Persistence.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Infrastructure.Persistence.Messaging;

public sealed partial class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionProvider> log)
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
            try
            {
                var c = await factory.CreateConnectionAsync("aijobs-worker", ct);
                c.ConnectionShutdownAsync += (_, e) => { LogRabbitMqConnectionLost(log, e.ReplyText); return Task.CompletedTask; };
                c.RecoverySucceededAsync += (_, _) => { LogRabbitMqConnectionRecovered(log, o.Host); return Task.CompletedTask; };
                LogRabbitMqConnected(log, o.Host, o.Port, o.VirtualHost);
                return _connection = c;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogRabbitMqConnectionFailed(log, o.Host, o.Port, o.VirtualHost, ex.GetType().Name, ex);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    [LoggerMessage(
        EventId = 601,
        Level = LogLevel.Information,
        Message = "RabbitMqConnected {Host} {Port} {VirtualHost}")]
    private static partial void LogRabbitMqConnected(
        ILogger logger, string host, int port, string virtualHost);

    [LoggerMessage(
        EventId = 602,
        Level = LogLevel.Warning,
        Message = "RabbitMqConnectionLost {Reason}")]
    private static partial void LogRabbitMqConnectionLost(
        ILogger logger, string reason);

    [LoggerMessage(
        EventId = 603,
        Level = LogLevel.Information,
        Message = "RabbitMqConnectionRecovered {Host}")]
    private static partial void LogRabbitMqConnectionRecovered(
        ILogger logger, string host);

    [LoggerMessage(
        EventId = 604,
        Level = LogLevel.Error,
        Message = "RabbitMqConnectionFailed {Host} {Port} {VirtualHost} {ExceptionType}")]
    private static partial void LogRabbitMqConnectionFailed(
        ILogger logger, string host, int port, string virtualHost, string exceptionType, Exception exception);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) { await _connection.CloseAsync(); _connection.Dispose(); }
    }
}