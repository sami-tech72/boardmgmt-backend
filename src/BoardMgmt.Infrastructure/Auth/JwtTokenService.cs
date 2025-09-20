using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BoardMgmt.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BoardMgmt.Infrastructure.Auth
{
    public sealed class JwtTokenService(IConfiguration config) : IJwtTokenService
    {
        public string CreateToken(string userId, string email, IEnumerable<string> roles, IEnumerable<Claim>? extraClaims = null)
        {
            var issuer = config["Jwt:Issuer"] ?? "BoardMgmt";
            var audience = config["Jwt:Audience"] ?? "BoardMgmt.Client";
            var key = config["Jwt:Key"] ?? "super-secret-key-change-me";

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId),
                new(JwtRegisteredClaimNames.Email, email),
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Email, email),
            };

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            if (extraClaims is not null)
                claims.AddRange(extraClaims);

            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
