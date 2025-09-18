using BoardMgmt.Application.Roles.Commands.AssignRole;
using BoardMgmt.Application.Roles.Commands.CreateRole;
using BoardMgmt.Application.Roles.Queries.GetRoles;
using BoardMgmt.Domain.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController(ISender mediator) : ControllerBase
    {
        [HttpGet]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<ActionResult<IReadOnlyList<string>>> Get()
            => Ok(await mediator.Send(new GetRolesQuery()));

        [HttpPost]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Create([FromBody] CreateRoleCommand cmd)
        {
            var ok = await mediator.Send(cmd);
            return ok ? Ok(new { message = "Role created (or already exists)" })
                      : Problem(title: "Failed to create role");
        }

        [HttpPost("assign")]
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Assign([FromBody] AssignRoleCommand cmd)
        {
            var ok = await mediator.Send(cmd);
            return ok ? Ok(new { message = "Role assigned" })
                      : Problem(title: "Failed to assign role");
        }
    }
}
