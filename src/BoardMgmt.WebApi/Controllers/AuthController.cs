using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Infrastructure.Persistence;
using BoardMgmt.WebApi.Common.Http;   // <= add
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IJwtTokenService _jwt;

    public AuthController(UserManager<AppUser> userManager, IJwtTokenService jwt)
    {
        _userManager = userManager;
        _jwt = jwt;
    }

    public record LoginDto(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return this.UnauthorizedApi("invalid_credentials", "Invalid email or password.");

        var ok = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!ok)
            return this.UnauthorizedApi("invalid_credentials", "Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwt.CreateToken(user.Id, user.Email!, roles);

        return this.OkApi(new { token }, "Login successful");
    }
}
