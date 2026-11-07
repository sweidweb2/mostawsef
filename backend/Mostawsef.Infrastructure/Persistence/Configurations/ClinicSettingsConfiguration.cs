using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class ClinicSettingsConfiguration : IEntityTypeConfiguration<ClinicSettings>
{
    public void Configure(EntityTypeBuilder<ClinicSettings> builder)
    {
        builder.ToTable("clinic_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(ClinicSettings.NameMaxLength).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(ClinicSettings.AddressMaxLength);
        builder.Property(s => s.Phone).HasMaxLength(ClinicSettings.PhoneMaxLength);
        builder.Property(s => s.CurrencyCode).HasMaxLength(ClinicSettings.CurrencyCodeLength).IsRequired();
    }
}
