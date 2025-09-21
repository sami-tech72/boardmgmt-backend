using System;

namespace BoardMgmt.Domain.Entities;

public class DocumentRoleAccess
{
    public Guid DocumentId { get; set; }
    public string RoleId { get; set; } = default!; // from AspNetRoles.Id

    public Document Document { get; set; } = default!;
}
