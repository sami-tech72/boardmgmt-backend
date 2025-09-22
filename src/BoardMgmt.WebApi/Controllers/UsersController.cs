//using BoardMgmt.Application.Users.Queries.Typeahead;
//using BoardMgmt.WebApi.Common.Http;
//using MediatR;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;

//namespace BoardMgmt.WebApi.Controllers;

//[ApiController]
//[Route("api/[controller]")]
//public class UsersController(ISender mediator) : ControllerBase
//{
//    // GET /api/users/search?q=ali&take=20
//    [HttpGet("search")]
//    [Authorize(Policy = "Users.View")] 
//    public async Task<IActionResult> Search(
//        [FromQuery(Name = "q")] string? query,   // supports ?q= as a common convention
//        [FromQuery] int take = 20,
//        CancellationToken ct = default)
//    {
//        // Guard / cap
//        if (string.IsNullOrWhiteSpace(query))
//            return this.OkApi(Array.Empty<TypeaheadUserDto>(), "Empty query");

//        // Soft cap to prevent abuse
//        if (take <= 0) take = 20;
//        if (take > 50) take = 50;

//        var items = await mediator.Send(new GetUsersTypeaheadQuery(query.Trim(), take), ct);
//        return this.OkApi(items, "Users search");
//    }
//}




using BoardMgmt.Application.Users.Queries.Typeahead;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ISender _mediator;
    public UsersController(ISender mediator) => _mediator = mediator;

    // GET /api/users/search?q=ali&take=20
    [HttpGet("search")]
    [Authorize(Policy = "Users.View")] // match your policy name
    public async Task<IActionResult> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Ok(Array.Empty<TypeaheadUserDto>());

        if (take <= 0) take = 20;
        if (take > 50) take = 50;

        var items = await _mediator.Send(new GetUsersTypeaheadQuery(query.Trim(), take), ct);
        return Ok(items);
    }
}
