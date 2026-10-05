using Domain.Common.Entities;
using Domain.Jobs.Entities;

namespace Domain.Jobs.Entites;

public class JobResult : Entity<Guid>
{
    public Guid JobId { get; private set; }
    public string Result { get; private set; } = null!;

    public virtual Job Job { get; private set; } = null!;

    private JobResult()
    { }

    public static JobResult Create(Guid jobId, string resultJson)
    {
        return new JobResult
        {
            JobId = jobId,
            Result = resultJson,
            CreatedAt = DateTime.UtcNow
        };
    }
}