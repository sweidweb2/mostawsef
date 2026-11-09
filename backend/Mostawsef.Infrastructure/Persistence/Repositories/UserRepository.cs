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

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken) =>
        db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, cancellationToken);

    public async Task<(IReadOnlyList<User> Items, int Total)> GetPageAsync(PageQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        var term = SearchText.Normalize(query.Search);
        if (term.Length > 0)
        {
            users = users.Where(u => u.SearchName.Contains(term) || u.Username.Contains(term));
        }

        var total = query.IncludeTotal
            ? await users.CountAsync(cancellationToken)
            : PageQuery.TotalNotComputed;
        var items = await users
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Username)
            .Skip((query.SafePage - 1) * query.SafePageSize)
            .Take(query.SafePageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(User user) => db.Users.Add(user);

    public void Remove(User user) => db.Users.Remove(user);

    public Task RecordLoginAsync(Guid userId, DateTime at, CancellationToken cancellationToken) =>
        db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, at), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
