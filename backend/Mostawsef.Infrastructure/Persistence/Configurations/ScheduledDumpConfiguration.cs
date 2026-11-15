using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class ScheduledDumpConfiguration : IEntityTypeConfiguration<ScheduledDump>
{
    public void Configure(EntityTypeBuilder<ScheduledDump> builder)
    {
        builder.ToTable("scheduled_dumps");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.FolderPath).HasMaxLength(ScheduledDump.FolderPathMaxLength).IsRequired();
        builder.Property(d => d.FilePath).HasMaxLength(ScheduledDump.FilePathMaxLength).IsRequired();
        builder.HasIndex(d => d.CreatedAt);
    }
}
