using BoardMgmt.Application.Roles.Commands.AssignRole;
using BoardMgmt.Application.Roles.Commands.DTOs;
using BoardMgmt.Application.Users.Commands.Login;
using BoardMgmt.Application.Users.Commands.Register;
using BoardMgmt.Application.Users.Queries.GetUsers;
using BoardMgmt.Domain.Auth;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ISender mediator) : ControllerBase
{
    // Users.View
    [HttpGet]
    [Authorize(Policy = "Users.View")]
    public async Task<IActionResult> GetAll()
    {
        var users = await mediator.Send(new GetUsersQuery());
        return this.OkApi(users, "Users loaded");
    }

    // Users.Create
    [HttpPost("register")]
    [Authorize(Policy = "Users.Create")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await mediator.Send(command);
        if (!result.Success)
            return this.BadRequestApi("registration_failed", result.Errors.FirstOrDefault() ?? "Registration failed");

        return this.OkApi(new { result.UserId, result.Email, result.FirstName, result.LastName }, "Registration successful");
    }

    // anonymous
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await mediator.Send(command);
        if (!result.Success)
            return this.UnauthorizedApi("invalid_credentials", result.Errors.FirstOrDefault() ?? "Invalid credentials");

        return this.OkApi(new { result.Token, result.UserId, result.Email, result.FullName }, "Login successful");
    }

    // keep Admin-only if you want
    [HttpPut("{id}/roles")]
    [Authorize(Policy = "Users.Page")]
    public async Task<IActionResult> AssignRoles(string id, [FromBody] AssignRolesBody body, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignRoleCommand(id, body.Roles), ct);
        if (!result.Success)
            return this.BadRequestApi("assign_roles_failed", string.Join("; ", result.Errors));

        return this.OkApi(new { userId = id, roles = result.AppliedRoles }, "Roles updated");
    }
}
