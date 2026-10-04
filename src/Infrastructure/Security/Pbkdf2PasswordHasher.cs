using Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : Application.IRepositories.IPasswordHasher
{
    private readonly PasswordHasher<User> _passwordHasher = new();
    private static readonly User HashUser = User.Create("hash-user", "hash@example.test", new string('x', 64));

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(HashUser, password);
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(HashUser, passwordHash, password);

        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
