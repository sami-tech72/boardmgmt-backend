using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Common.Email;

public interface IEmailSender
{
    Task SendAsync(
        string fromAddress,
        IEnumerable<string> toAddresses,
        string subject,
        string htmlBody,
        (string FileName, string ContentType, byte[] Bytes)? attachment = null,
        CancellationToken ct = default);
}
