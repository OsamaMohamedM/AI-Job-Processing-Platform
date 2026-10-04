using Domain.Common.Entities;

namespace Domain.Identity;

public class User : Entity<Guid>
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;

    public static User Create(string username, string email, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be null or empty.", nameof(passwordHash));
        if (passwordHash.Length < 64)
            throw new ArgumentException("Password hash must be at least 64 characters long.", nameof(passwordHash));
        email = email.Trim();
        username = username.Trim();
        passwordHash = passwordHash.Trim();

        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash
        };
    }
}