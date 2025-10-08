using System;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public class DocumentRoleAccess : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string RoleId { get; set; } = default!;

    public Document Document { get; set; } = default!;
}
