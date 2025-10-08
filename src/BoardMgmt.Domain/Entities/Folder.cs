using System;
using BoardMgmt.Domain.Common;

namespace BoardMgmt.Domain.Entities;

public class Folder : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public int DocumentCount { get; set; }
}
