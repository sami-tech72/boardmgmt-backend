using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BoardMgmt.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BoardMgmt.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public string CreateToken(string userId, string email, IEnumerable<string> roles, IEnumerable<Claim>? extraClaims = null)
    {
        var issuer = _config["Jwt:Issuer"] ?? "BoardMgmt";
        var audience = _config["Jwt:Audience"] ?? "BoardMgmt.Client";
        var keyString = _config["Jwt:Key"] ?? "super-secret-key-change-me";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (extraClaims is not null) claims.AddRange(extraClaims);

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
