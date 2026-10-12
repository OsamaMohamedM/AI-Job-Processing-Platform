using Application.Messaging;
using Infrastructure.Persistence.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages", t =>
        {
            t.HasCheckConstraint("CK_ProcessedMessages_Outcome", "[Outcome] IN ('Completed', 'Retried', 'DeadLettered', 'Skipped')");
        });

        builder.HasKey(m => m.MessageId);

        builder.Property(m => m.MessageId)
            .ValueGeneratedNever();

        builder.Property(m => m.ProcessedAt)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(m => m.Outcome)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
    }
}