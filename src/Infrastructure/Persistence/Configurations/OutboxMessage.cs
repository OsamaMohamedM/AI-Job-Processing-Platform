using Infrastructure.Persistence.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages", t =>
        {
            t.HasCheckConstraint("CK_OutboxMessages_Payload_IsJson", "ISJSON([Payload]) = 1");
            t.HasCheckConstraint("CK_OutboxMessages_AttemptCount", "[AttemptCount] >= 0");
        });

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.Type)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(m => m.AvailableAt)
            .HasPrecision(3)
            .IsRequired();

        builder.Property(m => m.PublishedAt)
            .HasPrecision(3);

        builder.Property(m => m.AttemptCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(m => m.LastError)
            .HasMaxLength(1000);

        builder.HasIndex(m => new { m.PublishedAt, m.AvailableAt })
            .HasDatabaseName("IX_OutboxMessages_PublishedAt_AvailableAt");
    }
}