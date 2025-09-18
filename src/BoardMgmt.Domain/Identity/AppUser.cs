

using Microsoft.AspNetCore.Identity;

namespace BoardMgmt.Domain.Entities;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}