using System.Linq;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Departments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Departments.Queries;

public sealed record GetDepartmentsQuery(string? Q = null, bool? ActiveOnly = null)
  : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed class GetDepartmentsHandler : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    private readonly IAppDbContext _db;

    public GetDepartmentsHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var q = _db.Departments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var text = request.Q.Trim().ToLowerInvariant();
            q = q.Where(d => d.Name.ToLower().Contains(text));
        }
        if (request.ActiveOnly == true) q = q.Where(d => d.IsActive);

        return await q.OrderBy(d => d.Name)
                      .Select(d => new DepartmentDto(d.Id, d.Name, d.Description, d.IsActive))
                      .ToListAsync(ct);
    }
}
