using Application.IRepositories;
using Domain.Jobs.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext db) : IJobRepository
{
    public void Add(Job job) => db.Jobs.Add(job);

    public Task<Job?> GetForProcessingAsync(Guid id, CancellationToken ct)
        => db.Jobs.Include(j => j.Attempts).FirstOrDefaultAsync(j => j.Id == id, ct);
}