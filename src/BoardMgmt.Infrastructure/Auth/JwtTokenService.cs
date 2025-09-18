using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BoardMgmt.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BoardMgmt.Infrastructure.Auth
{
    public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
    {
        public string CreateToken(string userId, string email, IEnumerable<string> roles)
        {
            var issuer = configuration["Jwt:Issuer"]!;
            var audience = configuration["Jwt:Audience"]!;
            var key = configuration["Jwt:Key"]!;
            var hours = double.TryParse(configuration["Jwt:ExpiryHours"], out var h) ? h : 24;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.Email, email ?? ""),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                                               SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(hours),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
