using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Domain.Calendars;
using BoardMgmt.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed partial class IngestTranscriptHandler
    {
    private async Task<int> IngestZoom(Meeting meeting, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("Zoom");
        var token = await _zoomTokenProvider.GetAccessTokenAsync(ct);

        var doc = await TryGetJson(
            http,
            token,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meeting.ExternalEventId!)}/recordings",
            ct);

        if (doc is not null)
            return await ExtractAndSaveZoomTranscriptOrThrow(meeting, http, token, doc, ct);

        var meetingDetail = await GetJsonOrThrow(
            http,
            token,
            $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meeting.ExternalEventId!)}",
            ct,
            on404: "Zoom didn’t recognize this meeting id. Verify the host and app scopes (meeting:read:admin).");

        var baseUuid = meetingDetail.RootElement.TryGetProperty("uuid", out var uuidEl)
            ? uuidEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(baseUuid))
            throw new InvalidOperationException("Zoom didn’t return a meeting UUID. Verify the meeting id and app scopes.");

        var instances = await GetJsonOrThrow(
            http,
            token,
            $"https://api.zoom.us/v2/past_meetings/{Uri.EscapeDataString(Uri.EscapeDataString(baseUuid))}/instances",
            ct,
            on404: "Zoom returned no past instances for this UUID. If you didn’t record to cloud, no transcript will exist.");

        if (!instances.RootElement.TryGetProperty("meetings", out var arr) ||
            arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No past instances returned for this meeting UUID. Was the meeting held and recorded to the cloud?");
        }

        foreach (var inst in arr.EnumerateArray())
        {
            var instUuid = inst.TryGetProperty("uuid", out var iu) ? iu.GetString() : null;
            if (string.IsNullOrWhiteSpace(instUuid)) continue;

            var safeUuid = Uri.EscapeDataString(Uri.EscapeDataString(instUuid));
            var recDoc = await TryGetJson(
                http,
                token,
                $"https://api.zoom.us/v2/past_meetings/{safeUuid}/recordings",
                ct);
            if (recDoc is null) continue;

            var saved = await TryExtractAndSaveZoomTranscript(meeting, http, token, recDoc, ct);
            if (saved.HasValue) return saved.Value;
        }

        throw new InvalidOperationException(
            "No cloud transcript file found for this meeting. " +
            "Enable 'Cloud recording' and 'Create audio transcript', record to cloud, wait for processing, then try again.");
    }

    private static async Task<JsonDocument?> TryGetJson(HttpClient http, string token, string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonDocument.Parse(json);
        }

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        var body = await resp.Content.ReadAsStringAsync(ct) ?? string.Empty;
        throw new HttpRequestException($"Zoom query failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {SummarizeZoomError(body)}");
    }

    private static async Task<JsonDocument> GetJsonOrThrow(HttpClient http, string token, string url, CancellationToken ct, string? on404 = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("Zoom returned an empty response body.");

            return JsonDocument.Parse(json);
        }

        var body = await resp.Content.ReadAsStringAsync(ct) ?? string.Empty;
        if (resp.StatusCode == HttpStatusCode.NotFound && !string.IsNullOrWhiteSpace(on404))
            throw new InvalidOperationException(on404);
        throw new HttpRequestException($"Zoom query failed ({(int)resp.StatusCode} {resp.ReasonPhrase}). Body: {SummarizeZoomError(body)}");
    }

    private async Task<int> ExtractAndSaveZoomTranscriptOrThrow(Meeting meeting, HttpClient http, string token, JsonDocument doc, CancellationToken ct)
    {
        var maybe = await TryExtractAndSaveZoomTranscript(meeting, http, token, doc, ct);
        if (maybe.HasValue) return maybe.Value;

        throw new InvalidOperationException(
            "No transcript file found in cloud recordings. " +
            "Enable 'Cloud recording' and 'Create audio transcript', record to cloud, and try again.");
    }

    private async Task<int?> TryExtractAndSaveZoomTranscript(Meeting meeting, HttpClient http, string token, JsonDocument doc, CancellationToken ct)
    {
        if (!doc.RootElement.TryGetProperty("recording_files", out var filesEl) || filesEl.ValueKind != JsonValueKind.Array)
            return null;

        var trFile = filesEl.EnumerateArray().FirstOrDefault(f =>
        {
            var type = f.TryGetProperty("file_type", out var t) ? t.GetString() : null;
            return type == "TRANSCRIPT" || type == "CC";
        });
        if (trFile.ValueKind == JsonValueKind.Undefined) return null;

        var fileId = trFile.GetProperty("id").GetString()!;
        var downloadUrl = trFile.GetProperty("download_url").GetString()!;

        var vtt = await DownloadZoomTranscriptAsync(http, token, downloadUrl, ct);
        if (string.IsNullOrWhiteSpace(vtt))
            throw new InvalidOperationException("Zoom returned an empty transcript content.");

        return await SaveVtt(meeting, CalendarProviders.Zoom, fileId, vtt, ct);
    }

    private async Task<string> DownloadZoomTranscriptAsync(HttpClient http, string token, string downloadUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException("Zoom recording download URL is missing.");

        const int MaxAttempts = 5;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var requestUrl = BuildZoomTranscriptDownloadUrl(downloadUrl, token);

                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.Accept.Clear();
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/vtt"));
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                req.Headers.AcceptEncoding.Clear();
                req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                req.Headers.ConnectionClose = true;
                req.Version = HttpVersion.Version11;
                req.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                return await reader.ReadToEndAsync(ct);
            }
            catch (TaskCanceledException ex)
            {
                if (ct.IsCancellationRequested)
                    throw;

                var ioEx = ex.InnerException as IOException;
                if (ioEx is not null && IsOperationAborted(ioEx))
                {
                    lastError = ioEx;
                    if (attempt == MaxAttempts)
                        break;

                    _logger.LogWarning(ioEx, "Transient I/O cancellation while downloading Zoom transcript (attempt {Attempt}/{MaxAttempts}).", attempt, MaxAttempts);
                    await Task.Delay(GetZoomDownloadRetryDelay(attempt), ct);
                    continue;
                }

                lastError = ex;
                if (attempt == MaxAttempts)
                    break;

                _logger.LogWarning(ex, "Transient cancellation while downloading Zoom transcript (attempt {Attempt}/{MaxAttempts}).", attempt, MaxAttempts);
                await Task.Delay(GetZoomDownloadRetryDelay(attempt), ct);
                continue;
            }
            catch (HttpRequestException ex) when (IsTransientStatusCode(ex.StatusCode) && attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Transient HTTP error while downloading Zoom transcript (attempt {Attempt}/{MaxAttempts}).", attempt, MaxAttempts);
                await Task.Delay(GetZoomDownloadRetryDelay(attempt), ct);
                continue;
            }
            catch (HttpRequestException ex) when (IsTransientStatusCode(ex.StatusCode))
            {
                lastError = ex;
                break;
            }
            catch (IOException ex) when (IsOperationAborted(ex) && attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "Transient I/O error while downloading Zoom transcript (attempt {Attempt}/{MaxAttempts}).", attempt, MaxAttempts);
                await Task.Delay(GetZoomDownloadRetryDelay(attempt), ct);
                continue;
            }
            catch (IOException ex) when (IsOperationAborted(ex))
            {
                lastError = ex;
                break;
            }
        }

        throw new InvalidOperationException("Failed to download Zoom transcript after multiple attempts.", lastError);
    }

    private static TimeSpan GetZoomDownloadRetryDelay(int attempt)
        => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

    private static string BuildZoomTranscriptDownloadUrl(string downloadUrl, string token)
    {
        if (downloadUrl.Contains("access_token=", StringComparison.OrdinalIgnoreCase))
            return downloadUrl;

        var separator = downloadUrl.Contains('?') ? '&' : '?';
        return $"{downloadUrl}{separator}access_token={Uri.EscapeDataString(token)}";
    }

    private static bool IsOperationAborted(IOException ex)
    {
        if (ex is null)
            return false;

        if (ex.HResult == unchecked((int)0x800703E3))
            return true;

        return ex.InnerException is SocketException { ErrorCode: 995 };
    }

    private static bool IsTransientStatusCode(HttpStatusCode? statusCode)
    {
        if (!statusCode.HasValue)
            return false;

        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == (HttpStatusCode)429
            || (int)statusCode >= 500;
    }

    private static string SummarizeZoomError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.ToString() : "";
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            return string.IsNullOrEmpty(code) && string.IsNullOrEmpty(msg) ? json : $"code={code}, message={msg}";
        }
        catch
        {
            return json;
        }
    }
    }
}
