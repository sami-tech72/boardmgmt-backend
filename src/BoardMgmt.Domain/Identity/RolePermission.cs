using System;
using BoardMgmt.Domain.Auth;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Identity;

public class RolePermission : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RoleId { get; set; } = default!;

    public AppModule Module { get; set; }

    public Permission Allowed { get; set; }
}
