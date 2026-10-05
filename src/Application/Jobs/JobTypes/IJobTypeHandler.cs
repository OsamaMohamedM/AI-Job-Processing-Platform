using Domain.Jobs.Enums;
using System.Text.Json;

namespace Application.Jobs.JobTypes;

public interface IJobTypeHandler
{
    JobType Type { get; }

    IReadOnlyDictionary<string, string[]>? ValidateInput(JsonElement input);
}