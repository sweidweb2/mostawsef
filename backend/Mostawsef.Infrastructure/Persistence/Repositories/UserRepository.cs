using Microsoft.EntityFrameworkCore;
using Mostawsef.Application.Interfaces;
using Mostawsef.Contracts.Common;
using Mostawsef.Domain.Common;
using Mostawsef.Domain.Entities;
using Mostawsef.Infrastructure.Persistence.Context;

namespace Mostawsef.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<UserDetailsRow?> GetDetailsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDetailsRow(
                u.Id, u.Username, u.FullName, u.Role, u.IsActive, u.LastLoginAt,
                u.CreatedAt, u.CreatedBy, u.UpdatedAt, u.UpdatedBy, u.Version))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<UserAuthStatus?> GetAuthStatusAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.IsActive, u.Role, u.TokenVersion })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new UserAuthStatus(row.IsActive, row.Role, row.TokenVersion);
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, cancellationToken);

    public async Task<(IReadOnlyList<UserListRow> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        var term = SearchText.Normalize(query.Search);
        if (term.Length > 0)
        {
            users = users.Where(u => u.SearchName.Contains(term) || u.Username.Contains(term));
        }

        var rows = users
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Username)
            .Select(u => new UserListRow(
                u.Id, u.Username, u.FullName, u.Role, u.IsActive, u.LastLoginAt, u.CreatedAt, u.Version));

        return await rows.ToPageAsync(query, cancellationToken);
    }

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);

    public Task RecordLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, at), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
