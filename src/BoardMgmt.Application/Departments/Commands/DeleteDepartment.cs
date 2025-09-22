using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DeleteDepartmentCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteDepartmentHandler(IAppDbContext db)
  : IRequestHandler<DeleteDepartmentCommand, bool>
{
    public async Task<bool> Handle(DeleteDepartmentCommand request, CancellationToken ct)
    {
        var d = await db.Departments.Include(x => x.Users)
                                    .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (d is null) return false;
        if (d.Users.Any())
            throw new InvalidOperationException("Cannot delete a department with assigned users.");

        db.Departments.Remove(d);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
