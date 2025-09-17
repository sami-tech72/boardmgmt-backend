using System.Collections.Immutable;

namespace BoardMgmt.Domain.Entities;

public static class DocumentAccessExtensions
{
    // Map flags → role names
    public static IReadOnlyList<string> ToRoles(this DocumentAccess access)
    {
        var roles = new List<string>(4);
        if ((access & DocumentAccess.Administrators) != 0) roles.Add(Auth.AppRoles.Admin);
        if ((access & DocumentAccess.BoardMembers) != 0) roles.Add(Auth.AppRoles.BoardMember);
        if ((access & DocumentAccess.CommitteeMembers) != 0) roles.Add(Auth.AppRoles.CommitteeMember);
        if ((access & DocumentAccess.Observers) != 0) roles.Add(Auth.AppRoles.Observer);
        return roles.ToImmutableArray();
    }

    // Map roles → flags
    public static DocumentAccess ToAccessMask(this IEnumerable<string> roles)
    {
        DocumentAccess mask = DocumentAccess.None;
        foreach (var r in roles)
        {
            switch (r)
            {
                case Auth.AppRoles.Admin: mask |= DocumentAccess.Administrators; break;
                case Auth.AppRoles.BoardMember: mask |= DocumentAccess.BoardMembers; break;
                case Auth.AppRoles.CommitteeMember: mask |= DocumentAccess.CommitteeMembers; break;
                case Auth.AppRoles.Observer: mask |= DocumentAccess.Observers; break;
            }
        }
        return mask;
    }
}
