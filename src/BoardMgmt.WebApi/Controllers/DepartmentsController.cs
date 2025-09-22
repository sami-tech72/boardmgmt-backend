using BoardMgmt.Application.Departments.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class DepartmentsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    //[Authorize(Policy = "Departments.View")]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll([FromQuery] string? q, [FromQuery] bool? activeOnly)
        => Ok(await mediator.Send(new GetDepartmentsQuery(q, activeOnly)));

    [HttpPost]
    //[Authorize(Policy = "Departments.Create")]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentDto dto)
        => Ok(await mediator.Send(new CreateDepartmentCommand(dto.Name, dto.Description)));

    [HttpPut("{id:guid}")]
    //[Authorize(Policy = "Departments.Edit")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, [FromBody] UpdateDepartmentDto dto)
    {
        if (id != dto.Id) return BadRequest("Mismatched id.");
        return Ok(await mediator.Send(new UpdateDepartmentCommand(dto.Id, dto.Name, dto.Description, dto.IsActive)));
    }

    [HttpDelete("{id:guid}")]
    //[Authorize(Policy = "Departments.Delete")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await mediator.Send(new DeleteDepartmentCommand(id));
        return ok ? NoContent() : NotFound();
    }
}
