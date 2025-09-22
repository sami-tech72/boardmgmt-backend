namespace BoardMgmt.Application.Departments.DTOs;

public sealed record DepartmentDto(Guid Id, string Name, string? Description, bool IsActive);
public sealed record CreateDepartmentDto(string Name, string? Description);
public sealed record UpdateDepartmentDto(Guid Id, string Name, string? Description, bool IsActive);
