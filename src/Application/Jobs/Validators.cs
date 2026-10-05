namespace Application.Jobs;

using Domain.Jobs.Enums;
using FluentValidation;
using global::AiJobPlatform.Application.Jobs;
using System.Text.Json;

public sealed class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Type).Must(t => EnumParser.TryParse<JobType>(t, out _)).WithMessage("Unknown job type.");
        RuleFor(x => x.Input).Must(i => i.ValueKind == JsonValueKind.Object).WithMessage("Input must be a JSON object.");
    }
}

public sealed class ListJobsQueryValidator : AbstractValidator<ListJobsQuery>
{
    public ListJobsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || EnumParser.TryParse<JobStatus>(s, out _)).WithMessage("Unknown status.");
        RuleFor(x => x.Type).Must(t => t is null || EnumParser.TryParse<JobType>(t, out _)).WithMessage("Unknown job type.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Order).Must(o => string.Equals(o, "asc", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(o, "desc", StringComparison.OrdinalIgnoreCase));
    }
}