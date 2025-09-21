using System;

namespace BoardMgmt.Domain.Entities;

public class DocumentRoleAccess
{
    


    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public string RoleId { get; set; } = default!;

    public Document Document { get; set; } = default!;


}
