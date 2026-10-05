using FluentValidation;

namespace AiJobPlatform.Api.Filter;

public class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var arg = ctx.Arguments.OfType<T>().FirstOrDefault()
                  ?? throw new BadHttpRequestException("Request body is required.");

        var validator = ctx.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(arg, ctx.HttpContext.RequestAborted);
        if (!result.IsValid)
            throw new ValidationException("Validation Error", result.Errors);

        return await next(ctx);
    }
}