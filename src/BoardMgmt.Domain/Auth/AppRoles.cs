namespace BoardMgmt.Domain.Auth;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string BoardMember = "BoardMember";
    public const string CommitteeMember = "CommitteeMember";
    public const string Observer = "Observer";
    public const string Secretary = "Secretary"; // used for meeting create auth

    public static readonly string[] All =
    {
        Admin, BoardMember, CommitteeMember, Observer, Secretary
    };
}
