using Application.Common;
using Domain.Jobs.Enums;

namespace Application.Jobs.JobTypes;

public sealed class JobTypeRegistry(IEnumerable<IJobTypeHandler> handlers)
{
    private readonly Dictionary<JobType, IJobTypeHandler> _map = handlers.ToDictionary(h => h.Type);

    public IJobTypeHandler Get(JobType type)
        => _map.TryGetValue(type, out var h) ? h : throw new UnsupportedJobTypeException(type);
}