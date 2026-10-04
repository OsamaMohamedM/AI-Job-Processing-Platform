using Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Configurations;

public class UserConfigurations : IEntityTypeConfiguration<User>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .ValueGeneratedOnAdd();
        builder.Property(u => u.Username)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(200);
        builder.HasIndex(u => u.Email)
            .IsUnique();
    }
}