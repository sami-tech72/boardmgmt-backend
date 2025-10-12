using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed partial class IngestTranscriptHandler
    {
    private const string GraphV1BaseUrl = "https://graph.microsoft.com/v1.0";
    private const string GraphBetaBaseUrl = "https://graph.microsoft.com/beta";
    private static readonly string[] TranscriptApiBaseUrls = { GraphV1BaseUrl, GraphBetaBaseUrl };

    private static readonly Dictionary<string, ParsableFactory<IParsable>> GraphErrorMapping = new()
    {
        {"4XX", ODataError.CreateFromDiscriminatorValue},
        {"5XX", ODataError.CreateFromDiscriminatorValue}
    };

    private static readonly Regex TeamsMeetingIdRegex = new(
        @"19(?:%3a|:)meeting[^\s/?""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private async Task<int> IngestTeams(Meeting meeting, CancellationToken ct)
    {
        var mailbox = !string.IsNullOrWhiteSpace(meeting.ExternalCalendarMailbox)
            ? meeting.ExternalCalendarMailbox
            : !string.IsNullOrWhiteSpace(meeting.HostIdentity)
                ? meeting.HostIdentity
                : _app.MailboxAddress;

        if (string.IsNullOrWhiteSpace(mailbox))
            throw new InvalidOperationException(
                "Meeting.ExternalCalendarMailbox is required for Teams transcript ingestion (set HostIdentity when creating the meeting or configure a default mailbox).");

        var onlineMeetingId = await ResolveTeamsOnlineMeetingIdAsync(mailbox!, meeting, ct);

        TeamsTranscriptMetadata? transcript;
        try
        {
            transcript = await GetTeamsTranscriptMetadataAsync(mailbox!, onlineMeetingId, ct);
        }
        catch (ServiceException ex) when (IsBadRequest(ex))
        {
            throw new InvalidOperationException($"Microsoft 365 reported that the transcript is not available yet. Wait for processing to finish and try again. Details: {GetGraphErrorDetail(ex)}", ex);
        }

        if (transcript is null)
            throw new InvalidOperationException("No transcript found for this Teams meeting. Ensure transcription was enabled.");

        await using var stream = await DownloadTeamsTranscriptContentAsync(mailbox!, onlineMeetingId, transcript.Id, ct)
            ?? throw new InvalidOperationException("Teams transcript download returned no content stream.");

        using var reader = new StreamReader(stream);
        var vtt = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(vtt))
            throw new InvalidOperationException("Teams returned an empty transcript content.");

        return await SaveVtt(meeting, "Microsoft365", transcript.Id, vtt, ct);
    }

    private async Task<TeamsTranscriptMetadata?> GetTeamsTranscriptMetadataAsync(
        string mailbox,
        string onlineMeetingId,
        CancellationToken ct)
    {
        try
        {
            return await ExecuteTranscriptRequestWithFallbackAsync(
                () => _graph.Users[mailbox]
                    .OnlineMeetings[onlineMeetingId]
                    .Transcripts
                    .ToGetRequestInformation(),
                async requestInfo =>
                {
                    await using var stream = await _graph.RequestAdapter.SendPrimitiveAsync<Stream>(
                        requestInfo,
                        GraphErrorMapping,
                        cancellationToken: ct);

                    if (stream is null)
                        return null;

                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    if (!doc.RootElement.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Array)
                        return null;

                    var transcripts = new List<TeamsTranscriptMetadata>();

                    foreach (var element in valueElement.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.Object)
                            continue;

                        var metadata = TryParseTeamsTranscriptMetadata(element);
                        if (metadata is not null)
                            transcripts.Add(metadata);
                    }

                    if (transcripts.Count == 0)
                    {
                        _logger.LogInformation(
                            "Teams transcript list returned no entries. Mailbox={Mailbox} OnlineMeetingId={OnlineMeetingId}",
                            mailbox,
                            onlineMeetingId);
                        return null;
                    }

                    var ready = transcripts
                        .Where(t => IsTranscriptReady(t.Status))
                        .OrderByDescending(t => t.CreatedUtc ?? DateTimeOffset.MinValue)
                        .ToList();

                    TeamsTranscriptMetadata selected;

                    if (ready.Count > 0)
                    {
                        selected = ready[0];
                    }
                    else
                    {
                        selected = transcripts
                            .OrderByDescending(t => t.CreatedUtc ?? DateTimeOffset.MinValue)
                            .First();
                    }

                    _logger.LogInformation(
                        "Selected Teams transcript {TranscriptId}. Status={Status} CreatedUtc={CreatedUtc}. TotalTranscripts={Count}",
                        selected.Id,
                        string.IsNullOrWhiteSpace(selected.Status) ? "(unknown)" : selected.Status,
                        selected.CreatedUtc?.ToString("O") ?? "(unknown)",
                        transcripts.Count);

                    return selected;
                });
        }
        catch (ServiceException ex)
        {
            LogGraphServiceException(ex, "Failed to list Teams transcripts", new { mailbox, onlineMeetingId });
            throw;
        }
    }

    private TeamsTranscriptMetadata? TryParseTeamsTranscriptMetadata(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
            return null;

        var id = idElement.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return null;

        DateTimeOffset? created = null;
        if (TryGetDateTimeOffset(element, "createdDateTime", out var createdValue))
            created = createdValue;

        var status = GetOptionalString(element, "status")
            ?? GetOptionalString(element, "state");

        return new TeamsTranscriptMetadata(id!, created, status);
    }

    private static bool TryGetDateTimeOffset(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.String)
        {
            var raw = property.GetString();
            if (!string.IsNullOrWhiteSpace(raw) &&
                DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                value = parsed;
                return true;
            }
        }
        else if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt64(out var seconds))
            {
                value = DateTimeOffset.FromUnixTimeSeconds(seconds);
                return true;
            }
        }

        return false;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.String)
        {
            var raw = property.GetString();
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        return null;
    }

    private static bool IsTranscriptReady(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;

        return status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("complete", StringComparison.OrdinalIgnoreCase)
            || status.Equals("ready", StringComparison.OrdinalIgnoreCase)
            || status.Equals("published", StringComparison.OrdinalIgnoreCase)
            || status.Equals("succeeded", StringComparison.OrdinalIgnoreCase)
            || status.Equals("success", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TeamsTranscriptMetadata(string Id, DateTimeOffset? CreatedUtc, string? Status);

    private async Task<Stream?> DownloadTeamsTranscriptContentAsync(
        string mailbox,
        string onlineMeetingId,
        string transcriptId,
        CancellationToken ct)
    {
        try
        {
            return await ExecuteTranscriptRequestWithFallbackAsync(
                () => _graph.Users[mailbox]
                    .OnlineMeetings[onlineMeetingId]
                    .Transcripts[transcriptId]
                    .Content
                    .ToGetRequestInformation(),
                requestInfo => _graph.RequestAdapter.SendPrimitiveAsync<Stream>(
                    requestInfo,
                    GraphErrorMapping,
                    cancellationToken: ct));
        }
        catch (ServiceException ex)
        {
            LogGraphServiceException(ex, "Failed to download Teams transcript content", new { mailbox, onlineMeetingId, transcriptId });
            throw;
        }
    }

    private async Task<T?> ExecuteTranscriptRequestWithFallbackAsync<T>(
        Func<RequestInformation> buildRequest,
        Func<RequestInformation, Task<T?>> sendAsync)
    {
        ServiceException? fallbackTrigger = null;

        foreach (var baseUrl in TranscriptApiBaseUrls)
        {
            var requestInfo = buildRequest();
            requestInfo.PathParameters["baseurl"] = baseUrl;

            try
            {
                return await sendAsync(requestInfo);
            }
            catch (ServiceException ex) when (baseUrl == GraphV1BaseUrl && ShouldRetryTranscriptRequest(ex))
            {
                fallbackTrigger = ex;
                _logger.LogDebug(
                    ex,
                    "Retrying Teams transcript request using beta endpoint because v1.0 returned StatusCode={StatusCode}. Detail: {Detail}",
                    ex.ResponseStatusCode,
                    GetGraphErrorDetail(ex));
                continue;
            }
        }

        if (fallbackTrigger is not null)
        {
            throw fallbackTrigger;
        }

        return default;
    }

    private static bool ShouldRetryTranscriptRequest(ServiceException ex)
    {
        if (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
        {
            return true;
        }

        if (ex.ResponseStatusCode == (int)HttpStatusCode.BadRequest)
        {
            return true;
        }

        if (ex.ResponseStatusCode >= 500 && ex.ResponseStatusCode < 600)
        {
            return true;
        }

        static bool MatchesRetryableGraphErrorCode(ServiceException exception)
        {
            if (exception.Error is null)
            {
                return false;
            }

            if (IsRetryableGraphErrorCode(exception.Error.Code))
            {
                return true;
            }

            var inner = exception.Error.InnerError;
            while (inner is not null)
            {
                if (IsRetryableGraphErrorCode(inner.Code))
                {
                    return true;
                }

                inner = inner.InnerError;
            }

            return false;
        }

        static bool IsRetryableGraphErrorCode(string? code)
            => !string.IsNullOrWhiteSpace(code)
                && (code.Equals("UnknownError", StringComparison.OrdinalIgnoreCase)
                    || code.Equals("generalException", StringComparison.OrdinalIgnoreCase)
                    || code.Equals("serverError", StringComparison.OrdinalIgnoreCase));

        return MatchesRetryableGraphErrorCode(ex);
    }

    private async Task<string> ResolveTeamsOnlineMeetingIdAsync(string mailbox, Meeting meeting, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
            throw new InvalidOperationException("Meeting.ExternalEventId not set.");

        Event? graphEvent = null;
        OnlineMeeting? meetingResource = null;

        try
        {
            meetingResource = await GetEventOnlineMeetingAsync(mailbox!, meeting.ExternalEventId!, ct);

            if (!string.IsNullOrWhiteSpace(meetingResource?.Id))
                return meetingResource!.Id;

            graphEvent = await _graph.Users[mailbox]
                .Events[meeting.ExternalEventId]
                .GetAsync(cfg =>
                {
                    cfg.QueryParameters.Select = new[] { "onlineMeeting" };
                }, ct);

            if (TryGetOnlineMeetingId(graphEvent, out var inlineId))
                return inlineId!;
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Teams meeting not found when resolving online meeting id. Verify the mailbox and meeting id.", ex);
        }
        catch (ServiceException ex)
        {
            LogGraphServiceException(ex, "Teams Graph API error while resolving online meeting id", new { mailbox, meeting.ExternalEventId });
            throw;
        }

        if (TryResolveOnlineMeetingIdFromJoinUrls(meeting, graphEvent, meetingResource, out var joinId))
            return joinId!;

        throw new InvalidOperationException("Teams meeting is missing an online meeting id. Ensure the event is a Teams meeting with transcription enabled.");
    }

    private Task<OnlineMeeting?> GetEventOnlineMeetingAsync(string mailbox, string eventId, CancellationToken ct)
    {
        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            UrlTemplate = "{+baseurl}/users/{user%2Did}/events/{event%2Did}/onlineMeeting",
        };

        requestInfo.PathParameters["user%2Did"] = mailbox;
        requestInfo.PathParameters["event%2Did"] = eventId;

        try
        {
            return _graph.RequestAdapter.SendAsync(
                requestInfo,
                OnlineMeeting.CreateFromDiscriminatorValue,
                cancellationToken: ct);
        }
        catch (ServiceException ex)
        {
            LogGraphServiceException(ex, "Failed to retrieve Teams online meeting", new { mailbox, eventId });
            throw;
        }
    }

    private void LogGraphServiceException(ServiceException ex, string context, object? data = null)
    {
        var detail = GetGraphErrorDetail(ex);

        if (data is not null)
        {
            _logger.LogError(ex, "{Context}. StatusCode: {StatusCode}. Detail: {Detail}. Data: {@Data}", context, ex.ResponseStatusCode, detail, data);
        }
        else
        {
            _logger.LogError(ex, "{Context}. StatusCode: {StatusCode}. Detail: {Detail}", context, ex.ResponseStatusCode, detail);
        }
    }

    private static bool IsBadRequest(ServiceException ex)
        => ex.ResponseStatusCode == (int)HttpStatusCode.BadRequest;

    private static string GetGraphErrorDetail(ServiceException ex)
    {
        var details = new List<string>();

        var (errorCode, errorMessage, rawResponse) = GetGraphErrorInfo(ex);

        if (!string.IsNullOrWhiteSpace(errorCode))
            details.Add($"Code: {errorCode}");

        if (!string.IsNullOrWhiteSpace(errorMessage))
            details.Add($"Message: {errorMessage}");

        if (!string.IsNullOrWhiteSpace(ex.Message))
            details.Add(ex.Message);

        if (!string.IsNullOrWhiteSpace(rawResponse))
        {
            var summary = TrySummarizeGraphRawResponse(rawResponse!);
            if (!string.IsNullOrWhiteSpace(summary))
                details.Add(summary!);
        }

        if (details.Count == 0)
        {
            if (ex.ResponseStatusCode is int statusCode)
            {
                details.Add($"Status {(HttpStatusCode)statusCode} ({statusCode})");
            }
            else
            {
                details.Add("Unknown Graph error");
            }
        }

        return string.Join("; ", details.Distinct(StringComparer.Ordinal));
    }

    private static (string? Code, string? Message, string? RawResponse) GetGraphErrorInfo(ServiceException ex)
    {
        string? code = null;
        string? message = null;
        string? rawResponse = null;

        var errorProp = ex.GetType().GetProperty("Error");
        if (errorProp != null)
        {
            var errorValue = errorProp.GetValue(ex);
            if (errorValue != null)
            {
                code = errorValue.GetType().GetProperty("Code")?.GetValue(errorValue) as string;
                message = errorValue.GetType().GetProperty("Message")?.GetValue(errorValue) as string;
            }
        }

        rawResponse = ex.GetType().GetProperty("RawResponseBody")?.GetValue(ex) as string
            ?? ex.GetType().GetProperty("ResponseBody")?.GetValue(ex) as string;

        return (code, message, rawResponse);
    }

    private static string? TrySummarizeGraphRawResponse(string rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("error", out var errorEl) && errorEl.ValueKind == JsonValueKind.Object)
                {
                    var code = errorEl.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String
                        ? codeEl.GetString()
                        : null;
                    var message = errorEl.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String
                        ? msgEl.GetString()
                        : null;
                    var inner = ExtractInnerErrorDetail(errorEl);

                    var parts = new List<string>();

                    if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(message))
                    {
                        var baseDetail = string.IsNullOrWhiteSpace(code)
                            ? message
                            : string.IsNullOrWhiteSpace(message)
                                ? code
                                : $"{code}: {message}";

                        if (!string.IsNullOrWhiteSpace(baseDetail))
                            parts.Add(baseDetail!);
                    }

                    if (!string.IsNullOrWhiteSpace(inner))
                        parts.Add(inner!);

                    if (parts.Count > 0)
                        return string.Join("; ", parts);
                }
            }
        }
        catch (JsonException)
        {
            // Ignore parsing issues and fall back to the raw response body.
        }

        return rawResponse.Length > 256
            ? rawResponse[..256] + "…"
            : rawResponse;
    }

    private static string? ExtractInnerErrorDetail(JsonElement errorElement)
    {
        if (!errorElement.TryGetProperty("innerError", out var inner) || inner.ValueKind != JsonValueKind.Object)
            return null;

        var properties = new List<string>();

        foreach (var property in inner.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
                properties.Add($"{property.Name}: {value}");
        }

        return properties.Count == 0
            ? null
            : "InnerError => " + string.Join(", ", properties);
    }

    private static bool TryGetOnlineMeetingId(Event? graphEvent, out string? id)
    {
        id = null;

        if (graphEvent?.OnlineMeeting?.AdditionalData != null &&
            graphEvent.OnlineMeeting.AdditionalData.TryGetValue("id", out var value) &&
            value is string str && !string.IsNullOrWhiteSpace(str))
        {
            id = str;
            return true;
        }

        if (graphEvent?.AdditionalData != null &&
            graphEvent.AdditionalData.TryGetValue("onlineMeetingId", out var rootId) &&
            rootId is string rootStr && !string.IsNullOrWhiteSpace(rootStr))
        {
            id = rootStr;
            return true;
        }

        if (graphEvent?.AdditionalData != null &&
            graphEvent.AdditionalData.TryGetValue("onlineMeeting", out var nested) &&
            nested is JsonElement el &&
            el.ValueKind == JsonValueKind.Object)
        {
            string? candidate = null;

            if (el.TryGetProperty("id", out var nestedIdEl) && nestedIdEl.ValueKind == JsonValueKind.String)
            {
                candidate = nestedIdEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(candidate) &&
                el.TryGetProperty("onlineMeetingId", out var nestedMeetingIdEl) &&
                nestedMeetingIdEl.ValueKind == JsonValueKind.String)
            {
                candidate = nestedMeetingIdEl.GetString();
            }

            if (!string.IsNullOrWhiteSpace(candidate))
            {
                id = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveOnlineMeetingIdFromJoinUrls(
        Meeting meeting,
        Event? graphEvent,
        OnlineMeeting? meetingResource,
        out string? id)
    {
        id = null;

        if (TryExtractOnlineMeetingId(meeting.OnlineJoinUrl, out id))
            return true;

        if (TryExtractOnlineMeetingId(GetJoinUrlFromEvent(graphEvent), out id))
            return true;

        if (TryExtractOnlineMeetingId(GetJoinUrlFromOnlineMeeting(meetingResource), out id))
            return true;

        return false;
    }

    private static bool TryExtractOnlineMeetingId(string? source, out string? id)
    {
        id = null;
        if (string.IsNullOrWhiteSpace(source))
            return false;

        var decoded = WebUtility.HtmlDecode(source);
        var match = TeamsMeetingIdRegex.Match(decoded ?? source);
        if (!match.Success)
            return false;

        var raw = match.Value;

        try
        {
            var unescaped = Uri.UnescapeDataString(raw);
            if (string.IsNullOrWhiteSpace(unescaped))
                return false;

            id = unescaped;
            return true;
        }
        catch (UriFormatException)
        {
            id = raw
                .Replace("%3a", ":", StringComparison.OrdinalIgnoreCase)
                .Replace("%40", "@", StringComparison.OrdinalIgnoreCase);
            return !string.IsNullOrWhiteSpace(id);
        }
    }

    private static string? GetJoinUrlFromEvent(Event? graphEvent)
    {
        if (!string.IsNullOrWhiteSpace(graphEvent?.OnlineMeeting?.JoinUrl))
            return graphEvent!.OnlineMeeting!.JoinUrl;

        if (graphEvent?.OnlineMeeting?.AdditionalData != null &&
            TryExtractAdditionalString(graphEvent.OnlineMeeting.AdditionalData, "joinUrl", out var inlineJoinUrl))
        {
            return inlineJoinUrl;
        }

        if (!string.IsNullOrWhiteSpace(graphEvent?.OnlineMeetingUrl))
            return graphEvent!.OnlineMeetingUrl;

        if (graphEvent?.AdditionalData != null)
        {
            if (TryExtractAdditionalString(graphEvent.AdditionalData, "onlineMeetingUrl", out var fromRoot))
                return fromRoot;

            if (graphEvent.AdditionalData.TryGetValue("onlineMeeting", out var nested) &&
                TryReadJoinUrlFromAdditionalData(nested, out var nestedJoinUrl))
            {
                return nestedJoinUrl;
            }
        }

        return null;
    }

    private static string? GetJoinUrlFromOnlineMeeting(OnlineMeeting? meetingResource)
    {
        if (meetingResource is null)
            return null;

        if (!string.IsNullOrWhiteSpace(meetingResource.JoinWebUrl))
            return meetingResource.JoinWebUrl;

        if (!string.IsNullOrWhiteSpace(meetingResource.JoinInformation?.Content))
            return meetingResource.JoinInformation.Content;

        if (meetingResource.AdditionalData != null)
        {
            if (TryExtractAdditionalString(meetingResource.AdditionalData, "joinWebUrl", out var joinWebUrl))
                return joinWebUrl;

            if (TryExtractAdditionalString(meetingResource.AdditionalData, "joinUrl", out var joinUrl))
                return joinUrl;

            if (meetingResource.AdditionalData.TryGetValue("joinInformation", out var nested) &&
                TryReadJoinUrlFromAdditionalData(nested, out var joinInfoContent))
            {
                return joinInfoContent;
            }
        }

        return null;
    }

    private static bool TryExtractAdditionalString(IDictionary<string, object?> data, string key, out string? value)
    {
        value = null;
        if (!data.TryGetValue(key, out var raw) || raw is null)
            return false;

        if (raw is string str && !string.IsNullOrWhiteSpace(str))
        {
            value = str;
            return true;
        }

        if (raw is JsonElement el && el.ValueKind == JsonValueKind.String)
        {
            var strVal = el.GetString();
            if (!string.IsNullOrWhiteSpace(strVal))
            {
                value = strVal;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadJoinUrlFromAdditionalData(object? nested, out string? value)
    {
        value = null;
        switch (nested)
        {
            case IDictionary<string, object?> dict when TryExtractAdditionalString(dict, "joinUrl", out var joinUrl):
                value = joinUrl;
                return true;
            case IDictionary<string, object?> dict when TryExtractAdditionalString(dict, "content", out var content):
                value = content;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.Object:
                if (element.TryGetProperty("joinUrl", out var joinUrlEl) && joinUrlEl.ValueKind == JsonValueKind.String)
                {
                    value = joinUrlEl.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                }

                if (element.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
                {
                    value = contentEl.GetString();
                    return !string.IsNullOrWhiteSpace(value);
                }
                break;
        }

        return false;
    }
    }
}
