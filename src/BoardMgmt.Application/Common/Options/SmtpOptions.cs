namespace BoardMgmt.Application.Common.Options;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;     // 587 = STARTTLS; 465 = implicit SSL
    public bool EnableSsl { get; set; } = true;

    public string? Username { get; set; }    // Optional if your server allows relay
    public string? Password { get; set; }

    public string? DefaultFrom { get; set; } // Used if caller passes empty From
    public string? FromDisplayName { get; set; } = "BoardMgmt";
}
