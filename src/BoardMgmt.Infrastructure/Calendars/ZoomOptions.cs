// Infrastructure/Calendars/ZoomOptions.cs
namespace BoardMgmt.Infrastructure.Calendars;


public sealed class ZoomOptions
{
    public string AccountId { get; set; } = default!; // From Zoom App (Server-to-Server OAuth)
    public string ClientId { get; set; } = default!; // From Zoom App
    public string ClientSecret { get; set; } = default!;// From Zoom App
    public string? HostUserId { get; set; } // default host (email or userId). Can be overridden by Meeting.ExternalCalendarMailbox
}