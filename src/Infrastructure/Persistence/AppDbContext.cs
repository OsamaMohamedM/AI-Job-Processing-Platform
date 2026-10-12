using Application.Common;
using Application.IRepositories;
using Application.Messaging;
using Domain.Identity;
using Domain.Jobs.Entites;
using Domain.Jobs.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<JobResult> JobResults => Set<JobResult>();
    public DbSet<JobAttempt> JobAttempts => Set<JobAttempt>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=OSOS\\OSAMA;Database=AIJob;Trusted_Connection=True;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public void ClearTracking() => ChangeTracker.Clear();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("A concurrency conflict occurred while updating the database.", ex);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new UniqueConstraintViolationException("A unique constraint violation occurred.", ex);
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("A concurrency conflict occurred while updating the database.", ex);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new UniqueConstraintViolationException("A unique constraint violation occurred.", ex);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is Microsoft.Data.SqlClient.SqlException sqlEx)
            {
                return sqlEx.Number is 2601 or 2627;
            }
        }
        return false;
    }
}