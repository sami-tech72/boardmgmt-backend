namespace BoardMgmt.Application.Common.Identity;

public sealed record MinimalIdentityUser(
    string Id,
    string? DisplayName,
    string? Email
);

public interface IIdentityUserReader
{
    Task<List<MinimalIdentityUser>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken ct);
}
