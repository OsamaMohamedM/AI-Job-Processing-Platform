using Domain.Jobs.Enums;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Application.Jobs.JobTypes;

public sealed class SummarizationJobType(IOptions<JobsOptions> options) : IJobTypeHandler
{
    public JobType Type => JobType.Summery;

    public IReadOnlyDictionary<string, string[]>? ValidateInput(JsonElement input)
        => TextInput.Validate(input, options.Value.MaxInputCharacters);
}