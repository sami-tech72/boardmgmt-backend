namespace BoardMgmt.Domain.Auth;

public static class AppRoles
{
    public const string Admin = "Admin";
    // add more if needed: public const string BoardMember = "BoardMember";
    public static readonly string[] All = [Admin /*, BoardMember */ ];
}
