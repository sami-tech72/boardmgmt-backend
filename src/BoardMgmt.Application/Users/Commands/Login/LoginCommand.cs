using MediatR;

namespace BoardMgmt.Application.Users.Commands.Login;

public record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public record LoginResponse(
    string Token,
    string UserId,
    string Email,
    string FullName,
    bool Success,
    IEnumerable<string> Errors);