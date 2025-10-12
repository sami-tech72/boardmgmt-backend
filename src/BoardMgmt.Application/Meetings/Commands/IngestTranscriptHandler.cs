using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BoardMgmt.Application.Calendars;
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Application.Common.Email;
using BoardMgmt.Application.Common.Options;
using BoardMgmt.Application.Common.Parsing; // SimpleVtt
using BoardMgmt.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace BoardMgmt.Application.Meetings.Commands
{
    public sealed class IngestTranscriptHandler(
        IAppDbContext db,
        GraphServiceClient graph,
        IHttpClientFactory httpFactory,
        IZoomTokenProvider zoomTokenProvider,
        IEmailSender email,
        IOptions<AppOptions> app,
        ILogger<IngestTranscriptHandler> logger)
        : IRequestHandler<IngestTranscriptCommand, int>
    {
        private readonly IAppDbContext _db = db;
        private readonly GraphServiceClient _graph = graph;
        private readonly IHttpClientFactory _httpFactory = httpFactory;
        private readonly IZoomTokenProvider _zoomTokenProvider = zoomTokenProvider;
        private readonly IEmailSender _email = email;
        private readonly AppOptions _app = app.Value ?? new AppOptions();
        private readonly ILogger<IngestTranscriptHandler> _logger = logger;
        private const string GraphBetaBaseUrl = "https://graph.microsoft.com/beta";
        private static readonly Dictionary<string, ParsableFactory<IParsable>> GraphErrorMapping = new()
        {
            {"4XX", ODataError.CreateFromDiscriminatorValue},
            {"5XX", ODataError.CreateFromDiscriminatorValue}
        };

        private static readonly Regex TeamsMeetingIdRegex = new(
                     @"19(?:%3a|:)meeting[^\s/?""'<>]+",
                     RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<int> Handle(IngestTranscriptCommand request, CancellationToken ct)
        {
            var meeting = await _db.Set<Meeting>()
                .Include(m => m.Attendees)
                .FirstOrDefaultAsync(m => m.Id == request.MeetingId, ct)
                ?? throw new InvalidOperationException("Meeting not found.");

            if (string.IsNullOrWhiteSpace(meeting.ExternalCalendar))
                throw new InvalidOperationException("Meeting.ExternalCalendar not set.");

            if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
                throw new InvalidOperationException("Meeting.ExternalEventId not set.");

            var count = meeting.ExternalCalendar switch
            {
                "Microsoft365" => await IngestTeams(meeting, ct),
                "Zoom" => await IngestZoom(meeting, ct),
                _ => throw new InvalidOperationException($"Unsupported provider: {meeting.ExternalCalendar}")
            };

            // After saving: load and email a summary + attach VTT
            await EmailTranscriptAsync(meeting, ct);

            return count;
        }

        // --------------------------------------------------------------------
        // Microsoft Teams (Graph)
        // --------------------------------------------------------------------
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

            // List transcripts (ONLINE MEETING scope)
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

            // Download VTT
            await using var stream = await DownloadTeamsTranscriptContentAsync(mailbox!, onlineMeetingId, transcript.Id, ct)
                ?? throw new InvalidOperationException("Teams transcript download returned no content stream.");

            using var reader = new System.IO.StreamReader(stream);
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
            var requestInfo = _graph.Users[mailbox]
                .OnlineMeetings[onlineMeetingId]
                .Transcripts
                .ToGetRequestInformation();

            requestInfo.PathParameters["baseurl"] = GraphBetaBaseUrl;

            try
            {
                await using var stream = await _graph.RequestAdapter.SendPrimitiveAsync<System.IO.Stream>(
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

        private Task<System.IO.Stream?> DownloadTeamsTranscriptContentAsync(
            string mailbox,
            string onlineMeetingId,
            string transcriptId,
            CancellationToken ct)
        {
            var requestInfo = _graph.Users[mailbox]
                .OnlineMeetings[onlineMeetingId]
                .Transcripts[transcriptId]
                .Content
                .ToGetRequestInformation();

            requestInfo.PathParameters["baseurl"] = GraphBetaBaseUrl;

            try
            {
                return _graph.RequestAdapter.SendPrimitiveAsync<System.IO.Stream>(
                    requestInfo,
                    GraphErrorMapping,
                    cancellationToken: ct);
            }
            catch (ServiceException ex)
            {
                LogGraphServiceException(ex, "Failed to download Teams transcript content", new { mailbox, onlineMeetingId, transcriptId });
                throw;
            }
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

        // --------------------------------------------------------------------
        // Zoom (Recordings API)
        // --------------------------------------------------------------------
        private async Task<int> IngestZoom(Meeting meeting, CancellationToken ct)
        {
            var http = _httpFactory.CreateClient("Zoom");
            var token = await _zoomTokenProvider.GetAccessTokenAsync(ct);

            // 1) Try recordings by meeting id
            var doc = await TryGetJson(
                http, token,
                $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meeting.ExternalEventId!)}/recordings",
                ct);

            if (doc is not null)
                return await ExtractAndSaveZoomTranscriptOrThrow(meeting, http, token, doc, ct);

            // 2) Get meeting details (for base UUID)
            var meetingDetail = await GetJsonOrThrow(
                http, token,
                $"https://api.zoom.us/v2/meetings/{Uri.EscapeDataString(meeting.ExternalEventId!)}",
                ct,
                on404: "Zoom didn’t recognize this meeting id. Verify the host and app scopes (meeting:read:admin).");

            var baseUuid = meetingDetail.RootElement.TryGetProperty("uuid", out var uuidEl)
                ? uuidEl.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(baseUuid))
                throw new InvalidOperationException("Zoom didn’t return a meeting UUID. Verify the meeting id and app scopes.");

            // 3) List past instances for UUID
            var instances = await GetJsonOrThrow(
                http, token,
                $"https://api.zoom.us/v2/past_meetings/{Uri.EscapeDataString(Uri.EscapeDataString(baseUuid))}/instances",
                ct,
                on404: "Zoom returned no past instances for this UUID. If you didn’t record to cloud, no transcript will exist.");

            if (!instances.RootElement.TryGetProperty("meetings", out var arr) ||
                arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
                throw new InvalidOperationException("No past instances returned for this meeting UUID. Was the meeting held and recorded to the cloud?");

            // 4) Try each instance's recordings
            foreach (var inst in arr.EnumerateArray())
            {
                var instUuid = inst.TryGetProperty("uuid", out var iu) ? iu.GetString() : null;
                if (string.IsNullOrWhiteSpace(instUuid)) continue;

                var safeUuid = Uri.EscapeDataString(Uri.EscapeDataString(instUuid));
                var recDoc = await TryGetJson(http, token,
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

        // -------------------- Zoom helpers --------------------
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
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
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
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound && !string.IsNullOrWhiteSpace(on404))
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

            return await SaveVtt(meeting, "Zoom", fileId, vtt, ct);
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
            catch { return json; }
        }

        // --------------------------------------------------------------------
        // Persist VTT → Transcript + TranscriptUtterance
        // --------------------------------------------------------------------
        private async Task<int> SaveVtt(Meeting meeting, string provider, string providerTranscriptId, string vtt, CancellationToken ct)
        {
            var cues = SimpleVtt.Parse(vtt).ToList();
            const int MaxAttempts = 3;

            return await SaveVttWithRetryAsync(meeting, provider, providerTranscriptId, cues, ct, attempt: 1, maxAttempts: MaxAttempts);
        }

        private async Task<int> SaveVttWithRetryAsync(
            Meeting meeting,
            string provider,
            string providerTranscriptId,
            IReadOnlyList<SimpleVtt.Cue> cues,
            CancellationToken ct,
            int attempt,
            int maxAttempts)
        {
            if (attempt > 1 && _db is DbContext dbContext)
            {
                dbContext.ChangeTracker.Clear();
            }

            try
            {
                var existing = await _db.Set<Transcript>()
                    .Include(t => t.Utterances)
                    .FirstOrDefaultAsync(t => t.MeetingId == meeting.Id && t.Provider == provider, ct);

                if (existing != null)
                {
                    if (existing.Utterances.Count > 0)
                    {
                        _db.Set<TranscriptUtterance>().RemoveRange(existing.Utterances);
                        existing.Utterances.Clear();
                    }

                    existing.ProviderTranscriptId = providerTranscriptId;
                    existing.CreatedUtc = DateTimeOffset.UtcNow;

                    foreach (var cue in cues)
                        existing.Utterances.Add(MapUtterance(meeting, existing, cue));

                    await _db.SaveChangesAsync(ct);
                    return existing.Utterances.Count;
                }

                var tr = new Transcript
                {
                    MeetingId = meeting.Id,
                    Provider = provider,
                    ProviderTranscriptId = providerTranscriptId,
                    CreatedUtc = DateTimeOffset.UtcNow
                };

                foreach (var cue in cues)
                    tr.Utterances.Add(MapUtterance(meeting, tr, cue));

                _db.Transcripts.Add(tr);
                await _db.SaveChangesAsync(ct);
                return tr.Utterances.Count;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict while saving transcript {ProviderTranscriptId} for meeting {MeetingId} using provider {Provider}. Retrying attempt {Attempt} of {MaxAttempts}.",
                    providerTranscriptId,
                    meeting.Id,
                    provider,
                    attempt,
                    maxAttempts);

                return await SaveVttWithRetryAsync(meeting, provider, providerTranscriptId, cues, ct, attempt + 1, maxAttempts);
            }

            throw new InvalidOperationException("Failed to save transcript after retrying due to concurrency conflicts.");
        }

        private const int MaxUtteranceTextLength = 4000;

        private static TranscriptUtterance MapUtterance(Meeting meeting, Transcript tr, SimpleVtt.Cue cue)
        {
            string? userId = null;
            string? email = cue.SpeakerEmail;

            var text = cue.Text;
            if (text.Length > MaxUtteranceTextLength)
            {
                // Avoid exceeding the database limit imposed by TranscriptUtterance.Text (nvarchar(4000)).
                // Keep as much of the speaker text as possible and add an ellipsis to indicate truncation.
                const string ellipsis = "…";
                var max = MaxUtteranceTextLength - ellipsis.Length;
                if (max < 0) max = 0;
                text = text[..max] + ellipsis;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                userId = meeting.Attendees
                    .FirstOrDefault(a => a.Email != null &&
                                         string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase))
                    ?.UserId;
            }

            if (userId is null && !string.IsNullOrWhiteSpace(cue.SpeakerName))
            {
                var byName = meeting.Attendees.FirstOrDefault(a =>
                    !string.IsNullOrWhiteSpace(a.Name) &&
                    string.Equals(a.Name, cue.SpeakerName, StringComparison.OrdinalIgnoreCase));
                userId ??= byName?.UserId;
                email ??= byName?.Email;
            }

            return new TranscriptUtterance
            {
                Transcript = tr,
                Start = cue.Start,
                End = cue.End,
                Text = text,
                SpeakerName = cue.SpeakerName,
                SpeakerEmail = email,
                UserId = userId
            };
        }

        // ---------------------------
        // Email the transcript (SMTP-friendly via IEmailSender)
        // ---------------------------
        private async Task EmailTranscriptAsync(Meeting meeting, CancellationToken ct)
        {
            var transcript = await _db.Set<Transcript>()
                .AsNoTracking()
                .Include(t => t.Utterances)
                .Where(t => t.MeetingId == meeting.Id)
                .OrderByDescending(t => t.CreatedUtc)
                .FirstOrDefaultAsync(ct);

            if (transcript is null) return;

            var lines = transcript.Utterances
                .OrderBy(u => u.Start)
                .Take(10)
                .Select(u =>
                {
                    var speaker = string.IsNullOrWhiteSpace(u.SpeakerName) ? (u.SpeakerEmail ?? "Unknown") : u.SpeakerName;
                    return $"<li><strong>{System.Net.WebUtility.HtmlEncode(speaker)}</strong>: {System.Net.WebUtility.HtmlEncode(u.Text)}</li>";
                });

            var meetingUrl = string.IsNullOrWhiteSpace(_app.AppBaseUrl)
                ? null
                : $"{_app.AppBaseUrl!.TrimEnd('/')}/meetings/{meeting.Id}/transcripts";

            var html = new StringBuilder()
                .Append("<!DOCTYPE html>")
                .Append("<html lang='en'>")
                .Append("<head>")
                .Append("<meta charset='UTF-8'>")
                .Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>")
                .Append("<style>")
                .Append("body { font-family: Arial, sans-serif; background-color: #f4f6f8; margin:0; padding:20px; }")
                .Append(".container { max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); padding:20px; }")
                .Append(".header { border-bottom: 2px solid #0078d4; padding-bottom: 10px; margin-bottom: 20px; }")
                .Append(".header h2 { margin:0; color:#0078d4; font-size:20px; }")
                .Append(".meta { font-size:14px; color:#555; margin-bottom: 20px; }")
                .Append(".preview { margin: 20px 0; }")
                .Append(".preview ol { padding-left:20px; }")
                .Append(".preview li { margin-bottom:8px; line-height:1.4; }")
                .Append(".footer { margin-top:30px; font-size:12px; color:#999; text-align:center; }")
                .Append(".btn { display:inline-block; padding:10px 15px; margin-top:15px; background:#0078d4; color:#fff; text-decoration:none; border-radius:4px; }")
                .Append("</style>")
                .Append("</head>")
                .Append("<body>")
                .Append("<div class='container'>")
                .Append("<div class='header'><h2>📄 Meeting Transcript</h2></div>")
                .Append("<div class='meta'>")
                .Append($"<p><strong>Title:</strong> {System.Net.WebUtility.HtmlEncode(meeting.Title)}</p>")
                .Append($"<p><strong>When:</strong> {meeting.ScheduledAt.LocalDateTime:G}</p>")
                .Append("</div>")
                .Append("<p>Hello,</p>")
                .Append("<p>The transcript for your meeting has been ingested successfully. Below is a short preview:</p>")
                .Append("<div class='preview'><ol>")
                .Append(string.Join("", lines))
                .Append("</ol></div>");

            if (!string.IsNullOrWhiteSpace(meetingUrl))
                html.Append($"<p><a href='{meetingUrl}' class='btn'>View Full Transcript</a></p>");

            html.Append("<div class='footer'>")
                .Append("<p>You are receiving this email because you attended this meeting.</p>")
                .Append("<p>&copy; BoardMgmt</p>")
                .Append("</div>")
                .Append("</div>")
                .Append("</body>")
                .Append("</html>");

            var vttBytes = BuildVttFromUtterances(transcript);
            var attachment = ("transcript.vtt", "text/vtt", vttBytes);

            var recipients = meeting.Attendees
                .Select(a => a.Email)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recipients.Count == 0) return;

            var subject = $"Transcript: {meeting.Title} ({meeting.ScheduledAt:yyyy-MM-dd})";

            var from = !string.IsNullOrWhiteSpace(meeting.ExternalCalendarMailbox)
                ? meeting.ExternalCalendarMailbox!
                : (_app.MailboxAddress ?? throw new InvalidOperationException("No sender mailbox configured (App:MailboxAddress)."));

            await _email.SendAsync(
                from,
                recipients,
                subject,
                html.ToString(),
                attachment,
                ct);
        }

        private static byte[] BuildVttFromUtterances(Transcript transcript)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WEBVTT");
            sb.AppendLine();

            foreach (var u in transcript.Utterances.OrderBy(x => x.Start))
            {
                static string fmt(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
                sb.AppendLine($"{fmt(u.Start)} --> {fmt(u.End)}");
                var speaker = string.IsNullOrWhiteSpace(u.SpeakerName) ? u.SpeakerEmail : u.SpeakerName;
                if (!string.IsNullOrWhiteSpace(speaker))
                    sb.AppendLine($"{speaker}: {u.Text}");
                else
                    sb.AppendLine(u.Text);
                sb.AppendLine();
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}