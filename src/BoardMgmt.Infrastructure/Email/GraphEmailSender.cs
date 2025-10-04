using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Common.Email;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace BoardMgmt.Infrastructure.Email;

public sealed class GraphEmailSender : IEmailSender
{
    private readonly GraphServiceClient _graph;

    public GraphEmailSender(GraphServiceClient graph) => _graph = graph;

    public async Task SendAsync(
        string fromAddress,
        IEnumerable<string> toAddresses,
        string subject,
        string htmlBody,
        (string FileName, string ContentType, byte[] Bytes)? attachment = null,
        CancellationToken ct = default)
    {
        var to = toAddresses
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(x => new Recipient { EmailAddress = new EmailAddress { Address = x } })
            .ToList();

        if (to.Count == 0) return;

        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody { ContentType = BodyType.Html, Content = htmlBody },
            ToRecipients = to
        };

        if (attachment is not null)
        {
            message.Attachments = new List<Attachment>
            {
                new FileAttachment
                {
                    OdataType = "#microsoft.graph.fileAttachment",
                    Name = attachment.Value.FileName,
                    ContentType = attachment.Value.ContentType,
                    ContentBytes = attachment.Value.Bytes
                }
            };
        }


        // Requires Mail.Send + ability to send as this mailbox
        await _graph.Users[fromAddress].SendMail.PostAsync(
            new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true
            },
            cancellationToken: ct);
    }
}
