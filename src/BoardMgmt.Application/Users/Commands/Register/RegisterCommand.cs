using MediatR;

namespace BoardMgmt.Application.Users.Commands.Register;

public record RegisterCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<RegisterResponse>;

public record RegisterResponse(
    string UserId,
    string Email,
    string FirstName,
    string LastName,
    bool Success,
    IEnumerable<string> Errors);