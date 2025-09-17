namespace BoardMgmt.Domain.Identity;

public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Keep IdentityRole (string id)
    public string RoleId { get; set; } = default!;

    public AppModule Module { get; set; }
    public Permission Allowed { get; set; }
}
