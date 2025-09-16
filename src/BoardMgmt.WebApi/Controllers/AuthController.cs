using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace BoardMgmt.WebApi.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userMgr;
    private readonly IJwtTokenService _jwt;


    public AuthController(UserManager<AppUser> userMgr, IJwtTokenService jwt)
    { _userMgr = userMgr; _jwt = jwt; }


    public record LoginRequest(string Email, string Password);


    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _userMgr.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized();
        if (!await _userMgr.CheckPasswordAsync(user, req.Password)) return Unauthorized();


        var roles = await _userMgr.GetRolesAsync(user);
        var token = _jwt.CreateToken(user.Id, user.Email!, roles);
        return Ok(new { token, email = user.Email, roles });
    }
}