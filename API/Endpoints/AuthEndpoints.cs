using Application.Identity;
using Application.Identity.Login;
using Application.Identity.Register;
using FluentValidation;

namespace API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IValidator<RegisterRequest> validator,
            RegisterUserHandler handler,
            CancellationToken cancellationToken) =>
        {
            await ValidateAsync(request, validator, cancellationToken);
            var response = await handler.HandleAsync(request, cancellationToken);

            return Results.Created($"/api/users/{response.UserId}", response);
        });

        group.MapPost("/login", async (
            LoginRequest request,
            IValidator<LoginRequest> validator,
            LoginHandler handler,
            CancellationToken cancellationToken) =>
        {
            await ValidateAsync(request, validator, cancellationToken);
            var response = await handler.HandleAsync(request, cancellationToken);

            return Results.Ok(response);
        });

        return app;
    }

    private static async Task ValidateAsync<T>(
        T request,
        IValidator<T> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (validationResult.IsValid)
        {
            return;
        }

        var errors = validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        throw new Application.Common.ValidationException(errors);
    }
}