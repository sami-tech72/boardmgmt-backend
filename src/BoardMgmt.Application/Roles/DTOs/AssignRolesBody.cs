namespace BoardMgmt.Application.Roles.Commands.DTOs;

public sealed class AssignRolesBody
{
    public List<string> Roles { get; set; } = new(); // role NAMES
}
