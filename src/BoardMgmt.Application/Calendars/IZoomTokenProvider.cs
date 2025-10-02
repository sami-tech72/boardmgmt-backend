using System.Threading;
using System.Threading.Tasks;

namespace BoardMgmt.Application.Calendars;

public interface IZoomTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
