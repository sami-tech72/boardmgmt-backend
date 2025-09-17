using BoardMgmt.Application.Common.Interfaces;
using System.Security.Claims;

namespace BoardMgmt.WebApi.Common.Security;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http)
    {
        _http = http;
    }

    public bool IsAuthenticated =>
        _http.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public string? UserId =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? Email =>
        _http.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyList<string> Roles =>
        _http.HttpContext?.User?
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray() // ✅ compact, avoids List allocation
        ?? Array.Empty<string>();
}
