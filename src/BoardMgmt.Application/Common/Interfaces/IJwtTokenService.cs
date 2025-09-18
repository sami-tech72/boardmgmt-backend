namespace BoardMgmt.Application.Common.Interfaces
{
    public interface IJwtTokenService
    {
        string CreateToken(string userId, string email, IEnumerable<string> roles);
    }
}
