// backend/src/BoardMgmt.Domain/Entities/Department.cs
namespace BoardMgmt.Domain.Entities;

public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public List<AppUser> Users { get; set; } = new();
}
