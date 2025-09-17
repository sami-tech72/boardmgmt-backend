using System.Text.RegularExpressions;

namespace BoardMgmt.Application.Common.Utilities;

public static class SlugHelper
{
    public static string Slugify(string s)
    {
        s = (s ?? string.Empty).ToLowerInvariant().Trim();
        s = Regex.Replace(s, @"[^\w\s-]", ""); // keep word/space/hyphen
        s = Regex.Replace(s, @"\s+", "-");     // spaces -> hyphens
        s = Regex.Replace(s, @"-+", "-");      // collapse ---
        return s;
    }
}
