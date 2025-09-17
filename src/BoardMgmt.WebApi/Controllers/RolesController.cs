using BoardMgmt.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    // Returns the role names your UI should display
    [HttpGet]
    [Authorize(Roles = AppRoles.Admin)] // or relax if you want all to see
    public ActionResult<IReadOnlyList<object>> Get()
    {
        // Display names if you want more friendly labels on UI
        var items = new[]
        {
            new { name = AppRoles.Admin,           display = "Administrators" },
            new { name = AppRoles.BoardMember,     display = "Board Members" },
            new { name = AppRoles.CommitteeMember, display = "Committee Members" },
            new { name = AppRoles.Observer,        display = "Observers" }
        };
        return Ok(items);
    }
}
