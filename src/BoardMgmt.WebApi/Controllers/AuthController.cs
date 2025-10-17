using BoardMgmt.Application.Roles.Commands.AssignRole;
using BoardMgmt.Application.Roles.Commands.DTOs;
using BoardMgmt.Application.Users.Commands.Login;
using BoardMgmt.Application.Users.Commands.Register;
using BoardMgmt.Application.Users.Commands.Update;
using BoardMgmt.WebApi.Auth;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// ⬇️ Make sure this matches where GetUsersQuery lives
using BoardMgmt.Application.Users.Queries; // e.g., GetUsersQuery namespace

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ISender mediator) : ControllerBase
{
    // GET /api/auth?q=&page=&pageSize=&activeOnly=&roles=Admin,BoardMember&departmentId=
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool? activeOnly = null,
        [FromQuery] string? roles = null,        // comma separated names
        [FromQuery] Guid? departmentId = null,
        CancellationToken ct = default
    )
    {
        var roleList = (roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var result = await mediator.Send(
            new GetUsersQuery(q, page, pageSize, activeOnly, roleList, departmentId),
            ct
        );

        // ✅ Return a paged envelope the Angular code expects
        return this.OkApi(new { items = result.Items, total = result.Total }, "Users loaded");
    }

    // GET /api/auth/search?query=ali&take=10
    // Returns minimal shape for autocomplete: [{ id, name, email }]
    [HttpGet("search")]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "query")] string? query,
        [FromQuery] int take = 10,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(query))
            return this.OkApi(Array.Empty<object>(), "No query");

        // Use page 1 with small pageSize=take; optionally pass activeOnly=true
        var res = await mediator.Send(new GetUsersQuery(query, 1, Math.Clamp(take, 1, 50), true, new List<string>(), null), ct);

        var minimal = res.Items.Select(u => new
        {
            id = u.Id.ToString(),
            name = u.FullName,
            email = u.Email
        }).ToList();

        return this.OkApi(minimal, "Search results");
    }

    // POST /api/auth/register
    [HttpPost("register")]
    [Authorize(Policy = PolicyNames.Users.Create)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await mediator.Send(command);
        if (!result.Success)
            return this.BadRequestApi("registration_failed", result.Errors.FirstOrDefault() ?? "Registration failed");

        return this.OkApi(new { result.UserId, result.Email, result.FirstName, result.LastName }, "Registration successful");
    }

    // POST /api/auth/login (anonymous)
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await mediator.Send(command);
        if (!result.Success)
            return this.UnauthorizedApi("invalid_credentials", result.Errors.FirstOrDefault() ?? "Invalid credentials");

        return this.OkApi(new { result.Token, result.UserId, result.Email, result.FullName }, "Login successful");
    }

    // PUT /api/auth/{id}/roles
    [HttpPut("{id}/roles")]
    [Authorize(Policy = PolicyNames.Users.Update)] // require ability to manage user assignments
    public async Task<IActionResult> AssignRoles(string id, [FromBody] AssignRolesBody body, CancellationToken ct)
    {
        var result = await mediator.Send(new AssignRoleCommand(id, body.Roles), ct);
        if (!result.Success)
            return this.BadRequestApi("assign_roles_failed", string.Join("; ", result.Errors));

        return this.OkApi(new { userId = id, roles = result.AppliedRoles }, "Roles updated");
    }

    public record AssignRolesBody(IReadOnlyList<string> Roles);

    public record UpdateUserBody(
        string? FirstName,
        string? LastName,
        string? Email,
        string? NewPassword,
        string? Role,
        Guid? DepartmentId,
        bool? IsActive
    );

    // PUT /api/auth/{id}
    [HttpPut("{id}")]
    [Authorize(Policy = PolicyNames.Users.Update)]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserBody body, CancellationToken ct)
    {
        var cmd = new UpdateUserCommand( // ✅ include id
            id,
            body.FirstName,
            body.LastName,
            body.Email,
            body.NewPassword,
            body.Role,
            body.DepartmentId,
            body.IsActive
        );

        var result = await mediator.Send(cmd, ct);
        if (!result.Success)
            return this.BadRequestApi("update_failed", string.Join("; ", result.Errors));

        return this.OkApi(new { result.UserId }, "User updated");
    }
}
