using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Departments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Departments.Commands;

public sealed record UpdateDepartmentCommand(Guid Id, string Name, string? Description, bool IsActive)
  : IRequest<DepartmentDto>;

public sealed class UpdateDepartmentHandler : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    private readonly IAppDbContext _db;

    public UpdateDepartmentHandler(IAppDbContext db) => _db = db;

    public async Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken ct)
    {
        var d = await _db.Departments.FirstOrDefaultAsync(x => x.Id == request.Id, ct)
                ?? throw new KeyNotFoundException("Department not found.");

        var name = request.Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var clash = await _db.Departments.AnyAsync(x => x.Id != d.Id && x.Name == name, ct);
        if (clash) throw new InvalidOperationException("Another department already has this name.");

        d.Name = name;
        d.Description = request.Description;
        d.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return new DepartmentDto(d.Id, d.Name, d.Description, d.IsActive);
    }
}
