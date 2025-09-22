using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Entities;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public sealed class AssignDepartmentHandler(UserManager<AppUser> userManager, IAppDbContext db)
  : IRequestHandler<AssignDepartmentCommand, bool>
{
    public async Task<bool> Handle(AssignDepartmentCommand request, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user is null) return false;

        if (request.DepartmentId.HasValue)
        {
            var exists = await db.Departments.AnyAsync(d => d.Id == request.DepartmentId.Value, ct);
            if (!exists) throw new KeyNotFoundException("Department not found.");
            user.DepartmentId = request.DepartmentId.Value;
        }
        else
        {
            user.DepartmentId = null; // clear
        }

        var res = await userManager.UpdateAsync(user);
        return res.Succeeded;
    }
}
