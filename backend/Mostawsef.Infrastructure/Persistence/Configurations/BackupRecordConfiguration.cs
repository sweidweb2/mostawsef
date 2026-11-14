using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;
using Mostawsef.Domain.Enums;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class BackupRecordConfiguration : IEntityTypeConfiguration<BackupRecord>
{
    public void Configure(EntityTypeBuilder<BackupRecord> builder)
    {
        builder.ToTable("backup_records");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Table).HasMaxLength(BackupRecord.TableMaxLength);
        builder.Property(b => b.Format).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(b => b.CreatedAt);
    }
}
