namespace Infrastructure.Persistence.Configurations;

using Domain.Jobs.Entites;
using Domain.Jobs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class JobAttemptConfiguration : IEntityTypeConfiguration<JobAttempt>
{
    public void Configure(EntityTypeBuilder<JobAttempt> builder)
    {
        builder.ToTable("JobAttempts", t =>
        {
            t.HasCheckConstraint("CK_JobAttempts_AttemptNumber", "[AttemptNumber] >= 1");
            t.HasCheckConstraint("CK_JobAttempts_Status", "[Status] IN ('Started', 'Succeeded', 'Failed', 'Abandoned')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.JobId)
            .IsRequired();

        builder.Property(a => a.AttemptNumber)
            .IsRequired();

        builder.Property(a => a.StartedAt)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(a => a.CompletedAt)
            .HasPrecision(3);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(a => a.ErrorMessage)
            .HasMaxLength(1000);

        builder.HasIndex(a => new { a.JobId, a.AttemptNumber })
            .IsUnique()
            .HasDatabaseName("UX_JobAttempts_JobId_AttemptNumber");

        builder.HasOne<Job>()
            .WithMany(j => j.Attempts)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}