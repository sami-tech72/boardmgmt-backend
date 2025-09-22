//namespace BoardMgmt.Application.Users.Queries.Typeahead;

//public sealed class TypeaheadUserDto
//{
//    public string Id { get; init; } = default!;
//    public string Name { get; init; } = default!;   // maps from UserName (or anything you prefer)
//    public string? Email { get; init; }
//}



namespace BoardMgmt.Application.Users.Queries.Typeahead;

public sealed class TypeaheadUserDto
{
    public string Id { get; init; } = default!;
    public string Name { get; init; } = default!;   // maps from UserName (or FullName if you have it)
    public string? Email { get; init; }
}
