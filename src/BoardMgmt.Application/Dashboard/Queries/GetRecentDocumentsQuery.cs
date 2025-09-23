using MediatR;
using BoardMgmt.Application.Dashboard.DTOs;
using BoardMgmt.Application.Common.Interfaces.Repositories;

namespace BoardMgmt.Application.Dashboard.Queries;

public record GetRecentDocumentsQuery(int Take = 3) : IRequest<IReadOnlyList<DashboardDocumentDto>>;

public class GetRecentDocumentsQueryHandler : IRequestHandler<GetRecentDocumentsQuery, IReadOnlyList<DashboardDocumentDto>>
{
    private readonly IDocumentReadRepository _repo;
    public GetRecentDocumentsQueryHandler(IDocumentReadRepository repo) => _repo = repo;

    public Task<IReadOnlyList<DashboardDocumentDto>> Handle(GetRecentDocumentsQuery request, CancellationToken ct)
        => _repo.GetRecentAsync(request.Take, ct);
}
