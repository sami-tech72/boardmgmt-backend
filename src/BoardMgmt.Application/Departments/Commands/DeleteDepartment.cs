using System.Linq;
using BoardMgmt.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Departments.Commands;

public sealed record DeleteDepartmentCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteDepartmentHandler : IRequestHandler<DeleteDepartmentCommand, bool>
{
    private readonly IAppDbContext _db;

    public DeleteDepartmentHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(DeleteDepartmentCommand request, CancellationToken ct)
    {
        var d = await _db.Departments.Include(x => x.Users)
                                     .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (d is null) return false;
        if (d.Users.Any())
            throw new InvalidOperationException("Cannot delete a department with assigned users.");

        _db.Departments.Remove(d);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
