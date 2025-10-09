using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Departments.DTOs;
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Departments.Commands;

public sealed record CreateDepartmentCommand(string Name, string? Description)
  : IRequest<DepartmentDto>;

public sealed class CreateDepartmentHandler : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    private readonly IAppDbContext _db;

    public CreateDepartmentHandler(IAppDbContext db) => _db = db;

    public async Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken ct)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var exists = await _db.Departments.AnyAsync(d => d.Name == name, ct);
        if (exists) throw new InvalidOperationException("Department with same name already exists.");

        var d = new Department { Name = name, Description = request.Description };
        _db.Departments.Add(d);
        await _db.SaveChangesAsync(ct);

        return new DepartmentDto(d.Id, d.Name, d.Description, d.IsActive);
    }
}
