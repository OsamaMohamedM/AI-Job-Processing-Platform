using Domain.Jobs.Enums;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Application.Jobs.JobTypes;

public sealed class ClassificationJobType(IOptions<JobsOptions> options) : IJobTypeHandler
{
    public JobType Type => JobType.Classification;

    public IReadOnlyDictionary<string, string[]>? ValidateInput(JsonElement input)
        => TextInput.Validate(input, options.Value.MaxInputCharacters);
}