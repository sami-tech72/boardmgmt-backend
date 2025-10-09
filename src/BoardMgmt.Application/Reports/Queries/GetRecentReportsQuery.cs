using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Reports.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Reports.Queries;

public record GetRecentReportsQuery(int Take = 10) : IRequest<List<RecentReportDto>>;

public sealed class GetRecentReportsHandler : IRequestHandler<GetRecentReportsQuery, List<RecentReportDto>>
{
    private readonly IAppDbContext _db;
    public GetRecentReportsHandler(IAppDbContext db) => _db = db;

    public async Task<List<RecentReportDto>> Handle(GetRecentReportsQuery request, CancellationToken ct)
    {
        return await _db.Set<Domain.Entities.GeneratedReport>()
            .AsNoTracking()
            .OrderByDescending(x => x.GeneratedAt)
            .Take(Math.Max(1, request.Take))
            .Select(x => new RecentReportDto(
                x.Id, x.Name, x.Type,
                x.GeneratedByUser!.DisplayName ?? x.GeneratedByUser!.UserName ?? "—",
                x.GeneratedAt, x.FileUrl, x.Format, x.PeriodLabel
            ))
            .ToListAsync(ct);
    }
}
