using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Email;
using BoardMgmt.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoardMgmt.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value ?? new SmtpOptions();
        _logger = logger;
    }

    public async Task SendAsync(
        string fromAddress,
        IEnumerable<string> toAddresses,
        string subject,
        string htmlBody,
        (string FileName, string ContentType, byte[] Bytes)? attachment = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var recipients = toAddresses
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning("SMTP send skipped: no recipients.");
            return;
        }

        var from = string.IsNullOrWhiteSpace(fromAddress) ? _options.DefaultFrom : fromAddress;
        if (string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("No From address provided and no Smtp:DefaultFrom configured.");

        using var msg = new MailMessage
        {
            From = new MailAddress(from!, _options.FromDisplayName),
            Subject = subject ?? string.Empty,
            Body = htmlBody ?? string.Empty,
            IsBodyHtml = true
        };

        foreach (var r in recipients)
            msg.To.Add(new MailAddress(r));

        if (attachment is not null)
        {
            var ms = new MemoryStream(attachment.Value.Bytes, writable: false);
            var att = new Attachment(ms, attachment.Value.FileName, attachment.Value.ContentType);
            msg.Attachments.Add(att);
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);

        // SmtpClient lacks token support; check once before sending.
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(msg);
    }
}
