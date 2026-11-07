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
        builder.Property(p => p.Notes).HasMaxLength(Patient.NotesMaxLength);
        builder.Property(p => p.IsInsured).IsRequired();
        builder.Property(p => p.Gender).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.HasIndex(p => p.SearchName);
        builder.HasIndex(p => p.Phone);
    }
}
