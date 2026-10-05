using Domain.Identity;
using Domain.Jobs.Entites;
using Domain.Jobs.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs", t =>
        {
            t.HasCheckConstraint("CK_Jobs_Status", "[Status] IN ('Pending','Queued','Processing','Retrying','Completed','Failed','DeadLettered')");
            t.HasCheckConstraint("CK_Jobs_Type", "[Type] IN ('Summarization','Classification')");
            t.HasCheckConstraint("CK_Jobs_Input_Json", "ISJSON([Input]) = 1");
            t.HasCheckConstraint("CK_Jobs_Attempts", "[AttemptCount] >= 0 AND [AttemptCount] <= [MaxAttempts] AND [MaxAttempts] >= 1");
            t.HasCheckConstraint("CK_Jobs_Completed", "(CASE WHEN [Status] = 'Completed' THEN 1 ELSE 0 END) = (CASE WHEN [CompletedAt] IS NOT NULL THEN 1 ELSE 0 END)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Input)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt)
            .HasPrecision(3);

        builder.Property(x => x.StartedAt)
            .HasPrecision(3);

        builder.Property(x => x.CompletedAt)
            .HasPrecision(3);

        builder.Property(x => x.FailedAt)
            .HasPrecision(3);

        builder.Property(x => x.LeaseExpiresAt)
            .HasPrecision(3);

        builder.Property<byte[]>("RowVersion")
            .IsRowVersion();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Attempts)
            .WithOne()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Result)
            .WithOne()
            .HasForeignKey<JobResult>(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Attempts)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt, x.Id })
            .IsDescending(false, true, true)
            .IncludeProperties(x => new { x.Status, x.Type })
            .HasDatabaseName("IX_Jobs_UserId_CreatedAt");
    }
}