namespace Infrastructure.Persistence.Configurations;

using Domain.Jobs.Entites;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class JobResultConfiguration : IEntityTypeConfiguration<JobResult>
{
    public void Configure(EntityTypeBuilder<JobResult> builder)
    {
        builder.ToTable("JobResults", t =>
        {
            t.HasCheckConstraint("CK_JobResults_Result_IsJson", "ISJSON([Result]) = 1");
        });

        builder.HasKey(r => r.JobId);

        builder.Property(r => r.JobId)
            .ValueGeneratedNever();

        builder.Property(r => r.Result)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasPrecision(3)
            .IsRequired();
        builder.HasIndex(r => r.JobId)
            .IsUnique()
            .HasDatabaseName("UX_JobResults_JobId");

        builder.HasOne(a => a.Job)
            .WithOne(j => j.Result)
            .HasForeignKey<JobResult>(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}