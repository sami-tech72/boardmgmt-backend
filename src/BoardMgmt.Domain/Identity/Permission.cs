using System;

namespace BoardMgmt.Domain.Identity
{
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

    public static class PermissionExtensions
    {
        private const Permission NonPage =
            Permission.View | Permission.Create | Permission.Update | Permission.Delete | Permission.Clone;

        // Domain invariant: any non-zero implies Page
        public static Permission Normalize(this Permission p)
        {
            if ((p & NonPage) != 0) p |= Permission.Page;
            return p;
        }

        public static bool Has(this Permission p, Permission needed) => (p & needed) == needed;
        public static int NormalizeInt(int mask) => (int)(((Permission)mask).Normalize());
    }
}
