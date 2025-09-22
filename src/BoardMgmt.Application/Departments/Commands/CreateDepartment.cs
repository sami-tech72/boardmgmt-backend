using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Departments.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateDepartmentCommand(string Name, string? Description)
  : IRequest<DepartmentDto>;

public sealed class CreateDepartmentHandler(IAppDbContext db)
  : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var exists = await db.Departments.AnyAsync(d => d.Name == name, ct);
        if (exists) throw new InvalidOperationException("Department with same name already exists.");

        var d = new Department { Name = name, Description = request.Description };
        db.Departments.Add(d);
        await db.SaveChangesAsync(ct);

        return new DepartmentDto(d.Id, d.Name, d.Description, d.IsActive);
    }
}
