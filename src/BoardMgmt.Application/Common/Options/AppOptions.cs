namespace BoardMgmt.Application.Common.Options;

public sealed class AppOptions
{
    public string? AppBaseUrl { get; set; }      // e.g., https://app.yourco.com
    public string? MailboxAddress { get; set; }  // fallback From if handler passes null/empty
}
