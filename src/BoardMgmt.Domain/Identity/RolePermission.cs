using BoardMgmt.Domain.Auth;

namespace BoardMgmt.Domain.Identity;

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // FK to AspNetRoles.Id
    public string RoleId { get; set; } = default!;

    // App area this permission applies to
    public AppModule Module { get; set; }

    // Bit flags: Permission enum
    public Permission Allowed { get; set; }
}
