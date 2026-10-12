using Application.Common;
using Application.IRepositories;
using Domain.Identity;

namespace Application.Identity.Register;

public sealed class RegisterUserHandler
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterUserHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<RegisterResponse> HandleAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("Email is already registered.");
        }

        var passwordHash = _passwordHasher.HashPassword(request.Password);
        var user = User.Create(normalizedEmail, normalizedEmail, passwordHash);

        _users.Add(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterResponse(user.Id, user.Email);
    }
}