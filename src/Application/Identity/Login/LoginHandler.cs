using System.Diagnostics;
using Application.Common;
using Application.IRepositories;
using Microsoft.Extensions.Logging;

namespace Application.Identity.Login;

public sealed class LoginHandler
{
    private const string DummyPasswordHash =
        "AQAAAAIAAYagAAAAEN7HpShfg0+wRgdjRfgkdTWGMETcgikR/zMWAox3lvTz49IcBbYAs8Sl0cPoMtmHvw==";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ICorrelationContext correlation,
        ILogger<LoginHandler> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task<LoginResponse> HandleAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var correlationId = _correlation.CorrelationId;
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
        var passwordHash = user?.PasswordHash ?? DummyPasswordHash;

        if (!_passwordHasher.VerifyPassword(passwordHash, request.Password) || user is null)
        {
            var durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            LogAuthenticationFailed(_logger, correlationId, durationMs, null);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var successDurationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        LogAuthenticationSucceeded(_logger, correlationId, user.Id, successDurationMs, null);
        return _tokenService.Generate(user);
    }

    private static readonly Action<ILogger, string, Guid, double, Exception?> LogAuthenticationSucceeded =
        LoggerMessage.Define<string, Guid, double>(
            LogLevel.Information,
            new EventId(201, "AuthenticationSucceeded"),
            "AuthenticationSucceeded {CorrelationId} {UserId} {DurationMs}");

    private static readonly Action<ILogger, string, double, Exception?> LogAuthenticationFailed =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(202, "AuthenticationFailed"),
            "AuthenticationFailed {CorrelationId} {DurationMs}");
}