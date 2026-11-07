using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
{
    public void Configure(EntityTypeBuilder<LabTest> builder)
    {
        builder.ToTable("lab_tests");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).HasMaxLength(LabTest.CodeMaxLength).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(LabTest.NameMaxLength).IsRequired();
        builder.Property(t => t.SearchName).HasMaxLength(LabTest.NameMaxLength).IsRequired();
        builder.Property(t => t.Price).IsRequired();
        builder.Property(t => t.LabCost).IsRequired();
        builder.Property(t => t.IsActive).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.SearchName).IsUnique();
    }
}
