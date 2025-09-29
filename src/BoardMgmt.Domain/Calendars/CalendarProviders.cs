// Domain/Calendars/CalendarProviders.cs
namespace BoardMgmt.Domain.Calendars;


public static class CalendarProviders
{
    public const string Microsoft365 = "Microsoft365";
    public const string Zoom = "Zoom";


    public static bool IsSupported(string? value) =>
    value is Microsoft365 or Zoom;
}