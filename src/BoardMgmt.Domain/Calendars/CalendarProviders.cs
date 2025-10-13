// Domain/Calendars/CalendarProviders.cs
using System;
using System.Linq;

namespace BoardMgmt.Domain.Calendars;


public static class CalendarProviders
{
    public const string Microsoft365 = "Microsoft365";
    public const string Zoom = "Zoom";

    private static readonly string[] Microsoft365Aliases =
    {
        Microsoft365,
        "Microsoft 365",
        "MS365",
        "MS 365",
        "Office365",
        "Office 365",
        "Teams",
        "Microsoft Teams"
    };


    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        if (Microsoft365Aliases.Any(alias => string.Equals(trimmed, alias, StringComparison.OrdinalIgnoreCase)))
            return Microsoft365;

        if (string.Equals(trimmed, Zoom, StringComparison.OrdinalIgnoreCase))
            return Zoom;

        return trimmed;
    }


    public static bool IsSupported(string? value)
    {
        var normalized = Normalize(value);
        return normalized is not null && (normalized == Microsoft365 || normalized == Zoom);
    }
}
