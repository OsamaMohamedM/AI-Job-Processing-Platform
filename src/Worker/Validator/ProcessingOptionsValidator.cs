namespace Worker.Validator;

using Application.AI;
using Application.Jobs.Processing;
using Microsoft.Extensions.Options;

public sealed class ProcessingOptionsValidator(IOptions<AiOptions> ai) : IValidateOptions<ProcessingOptions>
{
    public ValidateOptionsResult Validate(string? name, ProcessingOptions o)
        => o.LeaseSeconds < ai.Value.TimeoutSeconds + 15
            ? ValidateOptionsResult.Fail("Processing:LeaseSeconds must be at least Ai:TimeoutSeconds + 15.")
            : ValidateOptionsResult.Success;
}