using BoardMgmt.Application.Roles.Commands.AssignRole;
using BoardMgmt.Application.Roles.Commands.CreateRole;
using BoardMgmt.Application.Roles.Commands.SetRolePermissions;
using BoardMgmt.Application.Roles.Queries.GetRoles;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// ✅ add this alias:
using RP = BoardMgmt.Application.Roles.Commands.RenameRoleWithPermissions;

namespace BoardMgmt.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController(ISender mediator, IRoleService roles) : ControllerBase
    {
        [HttpGet]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<object>> Get(CancellationToken ct)
            => Ok(new { success = true, data = await mediator.Send(new GetRolesQuery(), ct) });

        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<object>> Create([FromBody] CreateRoleCommand cmd, CancellationToken ct)
        {
            var result = await mediator.Send(cmd, ct);
            return Ok(new { success = true, data = result, message = "Role created (or already exists)" });
        }

        [HttpPut("permissions")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<object>> SetPermissions([FromBody] SetRolePermissionsCommand cmd, CancellationToken ct)
        {
            var rows = await mediator.Send(cmd, ct);
            return Ok(new { success = true, data = rows, message = "Permissions saved" });
        }

        // ✅ rename + permissions in one call
        [HttpPut("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<object>> Update(string id, [FromBody] UpdateRoleBody body, CancellationToken ct)
        {
            var cmd = new RP.RenameRoleWithPermissionsCommand(id, body.Name, body.Items);
            await mediator.Send(cmd, ct);
            return Ok(new { success = true, message = "Role updated" });
        }

        // ✅ for edit prefill in UI
        [HttpGet("{id}/permissions")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<object>> GetPermissions(string id, CancellationToken ct)
        {
            var items = await roles.GetRolePermissionsAsync(id, ct);
            return Ok(new { success = true, data = items });
        }

        // ✅ delete role (+ its permissions)
        [HttpDelete("{id}")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<object>> Delete(string id, CancellationToken ct)
        {
            var (ok, errors) = await roles.DeleteRoleAsync(id, ct);
            if (!ok) return Problem(title: string.Join("; ", errors));
            return Ok(new { success = true, message = "Role deleted" });
        }

        // DTO the action binds to — must use the SAME type as the command
        public sealed class UpdateRoleBody
        {
            public string Name { get; set; } = string.Empty;
            public List<RP.PermissionDto> Items { get; set; } = new();
        }
    }
}
