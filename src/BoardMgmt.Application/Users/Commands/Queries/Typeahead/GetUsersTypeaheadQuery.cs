using MediatR;

namespace BoardMgmt.Application.Users.Queries.Typeahead;

public sealed record GetUsersTypeaheadQuery(string Query, int Take = 20)
    : IRequest<IReadOnlyList<TypeaheadUserDto>>;
