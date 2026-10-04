using Application.Common;
using Application.IRepositories;

namespace Application.Identity.Login;

public sealed class LoginHandler
{
    private const string DummyPasswordHash =
        "AQAAAAIAAYagAAAAEN7HpShfg0+wRgdjRfgkdTWGMETcgikR/zMWAox3lvTz49IcBbYAs8Sl0cPoMtmHvw==";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public LoginHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> HandleAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalizedEmail, cancellationToken);
        var passwordHash = user?.PasswordHash ?? DummyPasswordHash;

        if (!_passwordHasher.VerifyPassword(passwordHash, request.Password) || user is null)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        return _tokenService.Generate(user);
    }
}
