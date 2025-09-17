using BoardMgmt.Domain.Auth;  // where AppModule & Permission enums live

namespace BoardMgmt.Infrastructure.Persistence;

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RoleId { get; set; } = default!;
    public AppModule Module { get; set; }
    public Permission Allowed { get; set; }
}
