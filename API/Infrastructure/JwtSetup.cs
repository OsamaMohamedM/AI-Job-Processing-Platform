using System.Text;
using Application.Common;
using Application.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace API.Infrastructure;

public static class JwtSetup
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer),
                "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Audience is required.")
            .Validate(options => options.AccessTokenLifetimeMinutes > 0,
                "Jwt:AccessTokenLifetimeMinutes must be positive.")
            .ValidateOnStart();

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? new JwtOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteErrorAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "unauthorized",
                            "Authentication is required.");
                    },
                    OnForbidden = async context =>
                    {
                        await WriteErrorAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "forbidden",
                            "You are not allowed to access this resource.");
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    private static Task WriteErrorAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string message)
    {
        var traceId = httpContext.Items["CorrelationId"]?.ToString()
            ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        return httpContext.Response.WriteAsJsonAsync(
            new ApiError(code, message, traceId));
    }
}