using Application.Identity;
using Domain.Identity;

namespace Application.IRepositories;

public interface ITokenService
{
    LoginResponse Generate(User user);
}
