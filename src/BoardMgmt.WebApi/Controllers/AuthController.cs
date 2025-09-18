using BoardMgmt.Application.Users.Commands.Login;
using BoardMgmt.Application.Users.Commands.Register;
using BoardMgmt.WebApi.Common.Http;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ISender mediator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await mediator.Send(command);

        if (!result.Success)
        {
            return this.BadRequestApi("registration_failed", result.Errors.FirstOrDefault() ?? "Registration failed");
        }

        return this.OkApi(new
        {
            result.UserId,
            result.Email,
            result.FirstName,
            result.LastName
        }, "Registration successful");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await mediator.Send(command);

        if (!result.Success)
        {
            return this.UnauthorizedApi("invalid_credentials", result.Errors.FirstOrDefault() ?? "Invalid credentials");
        }

        return this.OkApi(new
        {
            result.Token,
            result.UserId,
            result.Email,
            result.FullName
        }, "Login successful");
    }
}