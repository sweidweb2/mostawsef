using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username)
            .HasMaxLength(User.UsernameMaxLength)
            .IsRequired();

        // Unique among deleted users too: usernames are never reused.
        builder.HasIndex(u => u.Username).IsUnique();

        builder.Property(u => u.FullName)
            .HasMaxLength(User.FullNameMaxLength)
            .IsRequired();

        builder.Property(u => u.SearchName)
            .HasMaxLength(User.FullNameMaxLength)
            .IsRequired();

        // List order (FullName) and search (SearchName.Contains); users stay few, but OFFSET pages still use these.
        builder.HasIndex(u => u.FullName);
        builder.HasIndex(u => u.SearchName);

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(u => u.TokenVersion).IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.IsActive).IsRequired();
    }
}
