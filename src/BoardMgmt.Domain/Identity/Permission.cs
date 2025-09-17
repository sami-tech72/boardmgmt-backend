namespace BoardMgmt.Domain.Identity;

[Flags]
public enum Permission : int
{
    None = 0,
    View = 1 << 0,
    Create = 1 << 1,
    Update = 1 << 2,
    Delete = 1 << 3,
    Page = 1 << 4,
    Clone = 1 << 5
}
