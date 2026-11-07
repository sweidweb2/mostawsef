using Microsoft.EntityFrameworkCore;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Context;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<SystemError> SystemErrors => Set<SystemError>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
