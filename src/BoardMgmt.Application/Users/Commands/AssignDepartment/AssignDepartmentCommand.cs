using MediatR;

public sealed record AssignDepartmentCommand(string UserId, Guid? DepartmentId) : IRequest<bool>;
