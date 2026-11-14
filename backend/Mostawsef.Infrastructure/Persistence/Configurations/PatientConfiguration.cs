using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FullName).HasMaxLength(Patient.FullNameMaxLength).IsRequired();
        builder.Property(p => p.SearchName).HasMaxLength(Patient.FullNameMaxLength).IsRequired();
        builder.Property(p => p.Phone).HasMaxLength(Patient.PhoneMaxLength).IsRequired();
        builder.Property(p => p.GuardianPhone).HasMaxLength(Patient.PhoneMaxLength);
        builder.Property(p => p.SearchText).HasMaxLength(Patient.SearchTextMaxLength).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(Patient.NotesMaxLength);
        builder.Property(p => p.IsInsured).IsRequired();
        builder.Property(p => p.Gender).HasConversion<string>().HasMaxLength(10).IsRequired();

        // Matches the list order (SearchName, then Id), so a page is read straight from the index without a sort.
        builder.HasIndex(p => new { p.SearchName, p.Id });
        builder.HasIndex(p => p.Phone);
        // Index-only PostgreSQL exception (SQLite ignores it): substring search stays plain Contains/LIKE.
        builder.HasIndex(p => p.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
