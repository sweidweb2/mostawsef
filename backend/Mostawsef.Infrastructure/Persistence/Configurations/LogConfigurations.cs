using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("activity_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.OccurredAt).IsRequired();
        builder.Property(l => l.Action).HasMaxLength(ActivityLog.ActionMaxLength).IsRequired();
        builder.Property(l => l.Module).HasMaxLength(ActivityLog.ModuleMaxLength).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(ActivityLog.DescriptionMaxLength).IsRequired();
        builder.Property(l => l.Username).HasMaxLength(ActivityLog.UsernameMaxLength);
        builder.Property(l => l.EntityType).HasMaxLength(ActivityLog.EntityMaxLength);
        builder.Property(l => l.EntityId).HasMaxLength(ActivityLog.EntityMaxLength);
        builder.Property(l => l.IpAddress).HasMaxLength(ActivityLog.IpAddressMaxLength);
        builder.Property(l => l.UserAgent).HasMaxLength(ActivityLog.UserAgentMaxLength);
        builder.Property(l => l.Details).HasMaxLength(ActivityLog.DetailsMaxLength);
        builder.Property(l => l.SearchText).HasMaxLength(ActivityLog.SearchTextMaxLength).IsRequired();

        builder.HasIndex(l => new { l.OccurredAt, l.Id });
        builder.HasIndex(l => new { l.Action, l.OccurredAt });
        builder.HasIndex(l => l.UserId);
        builder.HasIndex(l => l.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

public class SystemErrorConfiguration : IEntityTypeConfiguration<SystemError>
{
    public void Configure(EntityTypeBuilder<SystemError> builder)
    {
        builder.ToTable("system_errors");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Level).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Source).HasMaxLength(SystemError.SourceMaxLength).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(SystemError.MessageMaxLength).IsRequired();
        builder.Property(e => e.ExceptionType).HasMaxLength(SystemError.ExceptionTypeMaxLength);
        builder.Property(e => e.StackTrace).HasMaxLength(SystemError.StackTraceMaxLength);
        builder.Property(e => e.RequestPath).HasMaxLength(SystemError.RequestPathMaxLength);
        builder.Property(e => e.RequestMethod).HasMaxLength(SystemError.RequestMethodMaxLength);
        builder.Property(e => e.Username).HasMaxLength(SystemError.UsernameMaxLength);
        builder.Property(e => e.TraceId).HasMaxLength(SystemError.TraceIdMaxLength);
        builder.Property(e => e.IpAddress).HasMaxLength(SystemError.IpAddressMaxLength);
        builder.Property(e => e.Details).HasMaxLength(SystemError.DetailsMaxLength);
        builder.Property(e => e.SearchText).HasMaxLength(SystemError.SearchTextMaxLength).IsRequired();

        builder.HasIndex(e => new { e.OccurredAt, e.Id });
        builder.HasIndex(e => e.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}
