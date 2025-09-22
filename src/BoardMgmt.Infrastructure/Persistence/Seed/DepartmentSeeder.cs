using BoardMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Infrastructure.Persistence.Seed;

public static class DepartmentSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var defaults = new[]
        {
            new Department { Name = "Executive",       Description = "Executive leadership" },
            new Department { Name = "Finance",         Description = "Finance & accounting" },
            new Department { Name = "Legal",           Description = "Legal & compliance" },
            new Department { Name = "Operations",      Description = "Operations & IT" },
            new Department { Name = "Human Resources", Description = "HR" },
            new Department { Name = "Marketing",       Description = "Marketing & comms" }
        };

        foreach (var d in defaults)
        {
            if (!await db.Departments.AnyAsync(x => x.Name == d.Name))
                db.Departments.Add(d);
        }
        await db.SaveChangesAsync();
    }
}
