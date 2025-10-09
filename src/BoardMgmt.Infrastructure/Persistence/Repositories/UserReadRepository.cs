// Infrastructure/Persistence/Repositories/UserReadRepository.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using BoardMgmt.Application.Common.Interfaces.Repositories;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Domain.Entities;               // ✅ your AppUser
using Microsoft.EntityFrameworkCore;
using BoardMgmt.Infrastructure.Persistence;

namespace BoardMgmt.Infrastructure.Persistence.Repositories;

public class UserReadRepository : IUserReadRepository
{
    private readonly AppDbContext _db;
    public UserReadRepository(AppDbContext db) => _db = db;

    public Task<int> CountActiveAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        return _db.Set<AppUser>()
            .Where(u =>
                u.IsActive &&
                (u.LockoutEnd == null || u.LockoutEnd <= now))
            .CountAsync(ct);
    }

    public async Task<(int total, IReadOnlyList<ActiveUserItemDto> items)>
        GetActivePagedAsync(int page, int pageSize, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var baseQuery = _db.Set<AppUser>()
            .Where(u =>
                u.IsActive &&
                (u.LockoutEnd == null || u.LockoutEnd <= now));

        var total = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(u => u.DisplayName ?? u.UserName ?? u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new ActiveUserItemDto(
                u.Id,                                      // string key (IdentityUser)
                u.DisplayName ?? (u.FirstName + " " + u.LastName).Trim()
                    ?? u.UserName ?? u.Email ?? "User",
                u.Email,
                null                                       // you don’t store LastSeenUtc; leave null
            ))
            .ToListAsync(ct);

        return (total, items);
    }
}
