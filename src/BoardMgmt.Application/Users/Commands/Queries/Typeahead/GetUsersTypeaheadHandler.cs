//using BoardMgmt.Application.Users.Queries.Typeahead;
//using BoardMgmt.Domain.Entities;
//using BoardMgmt.Domain.Identity;
//using MediatR;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;

//namespace BoardMgmt.Application.Users.Queries.Typeahead;

//public sealed class GetUsersTypeaheadHandler
//    : IRequestHandler<GetUsersTypeaheadQuery, IReadOnlyList<TypeaheadUserDto>>
//{
//    private readonly UserManager<AppUser> _userManager;

//    public GetUsersTypeaheadHandler(UserManager<AppUser> userManager)
//        => _userManager = userManager;

//    public async Task<IReadOnlyList<TypeaheadUserDto>> Handle(
//        GetUsersTypeaheadQuery request,
//        CancellationToken ct)
//    {
//        var q = request.Query?.Trim().ToLowerInvariant();
//        if (string.IsNullOrWhiteSpace(q))
//            return Array.Empty<TypeaheadUserDto>();

//        // IQueryable<AppUser> -> EF translates where possible (if using EF store)
//        var usersQuery = _userManager.Users;

//        var items = await usersQuery
//            .Where(u =>
//                (u.UserName != null && u.UserName.ToLower().Contains(q)) ||
//                (u.Email != null && u.Email.ToLower().Contains(q)))
//            .OrderBy(u => u.UserName) // stable ordering for consistent typeahead UX
//            .Select(u => new TypeaheadUserDto
//            {
//                Id = u.Id,
//                Name = u.UserName!,     // adjust if you want FirstName + LastName
//                Email = u.Email
//            })
//            .Take(request.Take)
//            .AsNoTracking()
//            .ToListAsync(ct);

//        return items;
//    }
//}




using BoardMgmt.Application.Users.Queries.Typeahead;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BoardMgmt.Application.Users.Queries.Typeahead;

public sealed class GetUsersTypeaheadHandler
    : IRequestHandler<GetUsersTypeaheadQuery, IReadOnlyList<TypeaheadUserDto>>
{
    private readonly UserManager<AppUser> _userManager;

    public GetUsersTypeaheadHandler(UserManager<AppUser> userManager)
        => _userManager = userManager;

    public async Task<IReadOnlyList<TypeaheadUserDto>> Handle(
        GetUsersTypeaheadQuery request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return Array.Empty<TypeaheadUserDto>();

        var q = request.Query.Trim();

        // Start with the IQueryable from the store. Some providers won’t translate StringComparison;
        // we combine a basic filter and then refine in memory for correctness if needed.
        var queryable = _userManager.Users;

        // First-pass: broad filter (provider-translatable)
        var broad = queryable.Where(u =>
            (u.UserName != null && u.UserName.Contains(q)) ||
            (u.Email != null && u.Email.Contains(q)));

        // If EF Core is backing this, ToListAsync + AsNoTracking are fine.
        // If not EF, you can switch to .ToList() and remove AsNoTracking.
        var prelim = await broad
            .OrderBy(u => u.UserName)
#if NET8_0_OR_GREATER
            .AsNoTracking()
#endif
            .Take(Math.Max(50, request.Take)) // fetch a bit more for in-memory refine
            .ToListAsync(ct);

        // Refine in-memory using OrdinalIgnoreCase
        var items = prelim
            .Where(u =>
                (!string.IsNullOrEmpty(u.UserName) && u.UserName!.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email!.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(u => u.UserName)
            .Take(request.Take)
            .Select(u => new TypeaheadUserDto
            {
                Id = u.Id,
                Name = u.UserName ?? u.Email ?? u.Id,
                Email = u.Email
            })
            .ToList();

        return items;
    }
}
