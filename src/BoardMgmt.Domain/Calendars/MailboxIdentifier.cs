using System;
using System.Text.RegularExpressions;

namespace BoardMgmt.Domain.Calendars;

public static class MailboxIdentifier
{
    private static readonly Regex EmailInAngleBrackets = new("<([^>]+)>", RegexOptions.Compiled);

    private static readonly string[] KnownPrefixes =
    {
        "mailto:",
        "smtp:",
        "sip:",
        "userPrincipalName:",
        "upn:",
        "email:",
    };

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
            return null;

        // Handle comma or semicolon separated values by taking the first entry.
        var separatorIndex = FindSeparatorIndex(trimmed);
        if (separatorIndex > 0)
        {
            trimmed = trimmed[..separatorIndex].Trim();
        }

        // Handle display name formats such as "Name <user@domain>".
        var match = EmailInAngleBrackets.Match(trimmed);
        if (match.Success && match.Groups.Count > 1)
        {
            trimmed = match.Groups[1].Value.Trim();
        }

        // Remove wrapping quotes if present.
        trimmed = trimmed.Trim('"', '\'', '«', '»');

        foreach (var prefix in KnownPrefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[prefix.Length..].Trim();
                break;
            }
        }

        // If the identifier was encoded as URI, decode once.
        if (trimmed.Contains('%'))
        {
            try
            {
                trimmed = Uri.UnescapeDataString(trimmed);
            }
            catch (UriFormatException)
            {
                // Ignore invalid escape sequences and keep the original value.
            }
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private static int FindSeparatorIndex(string value)
    {
        var comma = value.IndexOf(',');
        var semicolon = value.IndexOf(';');

        if (comma < 0) return semicolon;
        if (semicolon < 0) return comma;
        return Math.Min(comma, semicolon);
    }
}
