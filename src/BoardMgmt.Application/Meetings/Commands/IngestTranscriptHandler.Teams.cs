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
using BoardMgmt.Domain.Calendars;
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

        // ------------- NEW: UPN/email → userId (GUID) -----------------
        private async Task<string> ResolveUserIdAsync(string mailboxOrUpn, CancellationToken ct)
        {
            if (Guid.TryParse(mailboxOrUpn, out _))
                return mailboxOrUpn;

            var user = await _graph.Users[mailboxOrUpn].GetAsync(cfg =>
            {
                cfg.QueryParameters.Select = new[] { "id" };
            }, ct);

            if (string.IsNullOrWhiteSpace(user?.Id))
                throw new InvalidOperationException($"Could not resolve user id for '{mailboxOrUpn}'.");

            return user!.Id!;
        }

        private async Task<int> IngestTeams(Meeting meeting, CancellationToken ct)
        {
            var rawMailbox = !string.IsNullOrWhiteSpace(meeting.ExternalCalendarMailbox)
                ? meeting.ExternalCalendarMailbox
                : !string.IsNullOrWhiteSpace(meeting.HostIdentity)
                    ? meeting.HostIdentity
                    : _app.MailboxAddress;

            var normalized = MailboxIdentifier.Normalize(rawMailbox);

            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException(
                    "Meeting.ExternalCalendarMailbox is required for Teams transcript ingestion (set HostIdentity when creating the meeting or configure a default mailbox).");

            if (!string.IsNullOrWhiteSpace(rawMailbox)
                && !string.Equals(rawMailbox, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "Normalized Teams mailbox identifier from {Original} to {Normalized} while ingesting transcript for meeting {MeetingId}.",
                    rawMailbox, normalized, meeting.Id);
            }

            // ALWAYS work with the AAD object id for /onlineMeetings & /transcripts
            var userId = await ResolveUserIdAsync(normalized!, ct);

            string onlineMeetingId;
            try
            {
                onlineMeetingId = await ResolveTeamsOnlineMeetingIdAsync(userId, meeting, ct); // pass userId
            }
            catch (ServiceException ex) when (IsTransientGraphServerError(ex))
            {
                throw new InvalidOperationException(
                    $"Microsoft 365 reported an internal error while retrieving the Teams meeting details. Wait for processing to finish and try again. Details: {GetGraphErrorDetail(ex)}",
                    ex);
            }
            catch (ServiceException ex)
            {
                throw new InvalidOperationException(
                    $"Microsoft 365 returned an unexpected error while retrieving the Teams meeting details. Details: {GetGraphErrorDetail(ex)}",
                    ex);
            }

            TeamsTranscriptMetadata? transcript;
            try
            {
                transcript = await GetTeamsTranscriptMetadataAsync(userId, onlineMeetingId, ct);
            }
            catch (ServiceException ex) when (IsBadRequest(ex))
            {
                throw new InvalidOperationException($"Microsoft 365 reported that the transcript is not available yet. Wait for processing to finish and try again. Details: {GetGraphErrorDetail(ex)}", ex);
            }
            catch (ServiceException ex) when (IsTransientGraphServerError(ex))
            {
                throw new InvalidOperationException($"Microsoft 365 reported an internal error while retrieving the Teams transcript. Wait for processing to finish and try again. Details: {GetGraphErrorDetail(ex)}", ex);
            }
            catch (ServiceException ex)
            {
                throw new InvalidOperationException($"Microsoft 365 returned an unexpected error while retrieving the Teams transcript metadata. Details: {GetGraphErrorDetail(ex)}", ex);
            }

            if (transcript is null)
                throw new InvalidOperationException("No transcript found for this Teams meeting. Ensure transcription was enabled.");

            Stream? stream;
            try
            {
                stream = await DownloadTeamsTranscriptContentAsync(userId, onlineMeetingId, transcript.Id, ct);
            }
            catch (ServiceException ex) when (IsTransientGraphServerError(ex))
            {
                throw new InvalidOperationException($"Microsoft 365 reported an internal error while downloading the Teams transcript content. Wait for processing to finish and try again. Details: {GetGraphErrorDetail(ex)}", ex);
            }
            catch (ServiceException ex)
            {
                throw new InvalidOperationException($"Microsoft 365 returned an unexpected error while downloading the Teams transcript content. Details: {GetGraphErrorDetail(ex)}", ex);
            }

            await using var contentStream = stream
                ?? throw new InvalidOperationException("Teams transcript download returned no content stream.");

            using var reader = new StreamReader(contentStream);
            var vtt = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(vtt))
                throw new InvalidOperationException("Teams returned an empty transcript content.");

            return await SaveVtt(meeting, CalendarProviders.Microsoft365, transcript.Id, vtt, ct);
        }

        private async Task<TeamsTranscriptMetadata?> GetTeamsTranscriptMetadataAsync(
            string userId,
            string onlineMeetingId,
            CancellationToken ct)
        {
            try
            {
                return await ExecuteTranscriptRequestWithFallbackAsync(
                    () =>
                    {
                        var request = _graph.Users[userId]
                            .OnlineMeetings[onlineMeetingId]
                            .Transcripts
                            .ToGetRequestInformation();

                        request.PathParameters["user%2Did"] = userId;                 // GUID
                        request.PathParameters["onlineMeeting%2Did"] = onlineMeetingId;
                        return request;
                    },
                    async requestInfo =>
                    {
                        var json = await _graph.RequestAdapter.SendPrimitiveAsync<string>(
                            requestInfo,
                            GraphErrorMapping,
                            cancellationToken: ct);

                        if (string.IsNullOrWhiteSpace(json))
                            return null;

                        using var doc = JsonDocument.Parse(json);
                        if (!doc.RootElement.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Array)
                            return null;

                        var transcripts = new List<TeamsTranscriptMetadata>();

                        foreach (var element in valueElement.EnumerateArray())
                        {
                            if (element.ValueKind != JsonValueKind.Object) continue;
                            var metadata = TryParseTeamsTranscriptMetadata(element);
                            if (metadata is not null) transcripts.Add(metadata);
                        }

                        if (transcripts.Count == 0)
                        {
                            _logger.LogInformation("Teams transcript list returned no entries. UserId={UserId} OnlineMeetingId={OnlineMeetingId}", userId, onlineMeetingId);
                            return null;
                        }

                        var ready = transcripts
                            .Where(t => IsTranscriptReady(t.Status))
                            .OrderByDescending(t => t.CreatedUtc ?? DateTimeOffset.MinValue)
                            .ToList();

                        var selected = ready.Count > 0
                            ? ready[0]
                            : transcripts.OrderByDescending(t => t.CreatedUtc ?? DateTimeOffset.MinValue).First();

                        _logger.LogInformation(
                            "Selected Teams transcript {TranscriptId}. Status={Status} CreatedUtc={CreatedUtc}. TotalTranscripts={Count}",
                            selected.Id,
                            string.IsNullOrWhiteSpace(selected.Status) ? "(unknown)" : selected.Status,
                            selected.CreatedUtc?.ToString("O") ?? "(unknown)",
                            transcripts.Count);

                        return selected;
                    });
            }
            catch (ServiceException ex) when (IsNotFound(ex))
            {
                throw new InvalidOperationException(
                    $"Microsoft 365 could not find a transcript for this Teams meeting. Details: {GetGraphErrorDetail(ex)}",
                    ex);
            }
            catch (ServiceException ex)
            {
                LogGraphServiceException(ex, "Failed to list Teams transcripts", new { userId, onlineMeetingId });
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
            string userId,
            string onlineMeetingId,
            string transcriptId,
            CancellationToken ct)
        {
            try
            {
                return await ExecuteTranscriptRequestWithFallbackAsync(
                    () =>
                    {
                        var request = _graph.Users[userId]
                            .OnlineMeetings[onlineMeetingId]
                            .Transcripts[transcriptId]
                            .Content
                            .ToGetRequestInformation();

                        request.PathParameters["user%2Did"] = userId;
                        request.PathParameters["onlineMeeting%2Did"] = onlineMeetingId;
                        request.PathParameters["teamsTranscript%2Did"] = transcriptId;
                        return request;
                    },
                    requestInfo => _graph.RequestAdapter.SendPrimitiveAsync<Stream>(
                        requestInfo,
                        GraphErrorMapping,
                        cancellationToken: ct));
            }
            catch (ServiceException ex) when (IsNotFound(ex))
            {
                throw new InvalidOperationException(
                    $"Microsoft 365 reported that the transcript content is no longer available. Try reprocessing the meeting or verify transcript retention settings. Details: {GetGraphErrorDetail(ex)}",
                    ex);
            }
            catch (ServiceException ex)
            {
                LogGraphServiceException(ex, "Failed to download Teams transcript content", new { userId, onlineMeetingId, transcriptId });
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
            if (ex.ResponseStatusCode == (int)HttpStatusCode.NotFound) return true;
            if (ex.ResponseStatusCode == (int)HttpStatusCode.BadRequest) return true;

            if (ex.ResponseStatusCode == (int)HttpStatusCode.Forbidden && IsPreviewAccessDenied(ex))
                return true;

            if (ex.ResponseStatusCode >= 500 && ex.ResponseStatusCode < 600) return true;

            foreach (var code in GetGraphErrorCodes(ex))
                if (IsRetryableGraphErrorCode(code)) return true;

            return false;
        }

        private static bool IsRetryableGraphErrorCode(string? code)
            => !string.IsNullOrWhiteSpace(code)
                && (code.Equals("UnknownError", StringComparison.OrdinalIgnoreCase)
                    || code.Equals("generalException", StringComparison.OrdinalIgnoreCase)
                    || code.Equals("serverError", StringComparison.OrdinalIgnoreCase));

        private static bool IsPreviewAccessDenied(ServiceException ex)
        {
            var (code, message, rawResponse) = GetGraphErrorInfo(ex);

            if (ContainsPreviewKeyword(message)) return true;

            if (string.Equals(code, "AccessDenied", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsPreviewKeyword(rawResponse)) return true;
                if (ContainsPreviewKeyword(message)) return true;
            }

            if (ContainsPreviewKeyword(rawResponse)) return true;

            return false;
        }

        private static bool ContainsPreviewKeyword(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            return value.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("beta", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("not supported in v1.0", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("not available in v1.0", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("use the /beta endpoint", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private async Task<string> ResolveTeamsOnlineMeetingIdAsync(string userId, Meeting meeting, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(meeting.ExternalOnlineMeetingId))
                return meeting.ExternalOnlineMeetingId!;

            if (string.IsNullOrWhiteSpace(meeting.ExternalEventId))
                throw new InvalidOperationException("Meeting.ExternalEventId not set.");

            Event? graphEvent = null;
            OnlineMeeting? meetingResource = null;

            try
            {
                meetingResource = await GetEventOnlineMeetingAsync(userId, meeting.ExternalEventId!, ct);
                if (!string.IsNullOrWhiteSpace(meetingResource?.Id))
                    return meetingResource!.Id;

                graphEvent = await _graph.Users[userId]
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
                throw new InvalidOperationException("Teams meeting not found when resolving online meeting id. Verify the user and meeting id.", ex);
            }
            catch (ServiceException ex)
            {
                LogGraphServiceException(ex, "Teams Graph API error while resolving online meeting id", new { userId, meeting.ExternalEventId });

                if (IsTeamsOnlineMeetingProvisioningIncomplete(ex))
                {
                    throw new InvalidOperationException(
                        "Microsoft 365 reported that the organizer's Teams account is not fully provisioned for online meetings. Ask the organizer to sign in to Microsoft Teams at least once and ensure a Teams license and meeting policy that allows online meetings are assigned.",
                        ex);
                }

                throw;
            }

            if (TryResolveOnlineMeetingIdFromJoinUrls(meeting, graphEvent, meetingResource, out var joinId))
                return joinId!;

            throw new InvalidOperationException("Teams meeting is missing an online meeting id. Ensure the event is a Teams meeting with transcription enabled.");
        }

        private Task<OnlineMeeting?> GetEventOnlineMeetingAsync(string userId, string eventId, CancellationToken ct)
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.GET,
                UrlTemplate = "{+baseurl}/users/{user%2Did}/events/{event%2Did}/onlineMeeting",
            };

            requestInfo.PathParameters["user%2Did"] = userId;   // GUID only
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
                LogGraphServiceException(ex, "Failed to retrieve Teams online meeting", new { userId, eventId });

                if (IsTeamsOnlineMeetingProvisioningIncomplete(ex))
                {
                    throw new InvalidOperationException(
                        "Microsoft 365 reported that the organizer's Teams account is not fully provisioned for online meetings.",
                        ex);
                }

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

        private static bool IsNotFound(ServiceException ex)
            => ex.ResponseStatusCode == (int)HttpStatusCode.NotFound;

        private static bool IsTransientGraphServerError(ServiceException ex)
        {
            if (ex.ResponseStatusCode is int statusCode && statusCode >= 500 && statusCode < 600) return true;

            foreach (var code in GetGraphErrorCodes(ex))
                if (IsRetryableGraphErrorCode(code)) return true;

            return false;
        }

        private static string GetGraphErrorDetail(ServiceException ex)
        {
            var details = new List<string>();

            var (errorCode, errorMessage, rawResponse) = GetGraphErrorInfo(ex);

            if (!string.IsNullOrWhiteSpace(errorCode)) details.Add($"Code: {errorCode}");
            if (!string.IsNullOrWhiteSpace(errorMessage)) details.Add($"Message: {errorMessage}");
            if (!string.IsNullOrWhiteSpace(ex.Message)) details.Add(ex.Message);

            if (!string.IsNullOrWhiteSpace(rawResponse))
            {
                var summary = TrySummarizeGraphRawResponse(rawResponse!);
                if (!string.IsNullOrWhiteSpace(summary)) details.Add(summary!);
            }

            if (details.Count == 0)
            {
                if (ex.ResponseStatusCode is int status)
                    details.Add($"Status {(HttpStatusCode)status} ({status})");
                else
                    details.Add("Unknown Graph error");
            }

            return string.Join("; ", details.Distinct(StringComparer.Ordinal));
        }

        private static bool IsTeamsOnlineMeetingProvisioningIncomplete(ServiceException ex)
        {
            var codes = GetGraphErrorCodes(ex);
            if (!codes.Contains("UnknownError") && !codes.Contains("generalException"))
                return false;

            var (_, message, rawResponse) = GetGraphErrorInfo(ex);
            var parts = new List<string?> { message, rawResponse, ex.Message };
            var combined = string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s))).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(combined)) return false;

            if (combined.Contains("teams") && combined.Contains("license")) return true;
            if (combined.Contains("teams") && combined.Contains("enabled")) return true;
            if (combined.Contains("enable teams")) return true;

            if ((combined.Contains("online meeting") || combined.Contains("onlinemeeting")) &&
                (combined.Contains("not available") || combined.Contains("not enabled") || combined.Contains("disabled")))
                return true;

            return false;
        }

        private static IReadOnlyCollection<string> GetGraphErrorCodes(ServiceException ex)
        {
            var codes = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

            var (code, _, rawResponse) = GetGraphErrorInfo(ex);

            if (!string.IsNullOrWhiteSpace(code)) codes.Add(code!);

            if (!string.IsNullOrWhiteSpace(rawResponse))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawResponse);
                    CollectGraphErrorCodes(doc.RootElement, codes);
                }
                catch (JsonException)
                {
                    // Ignore parsing issues.
                }
            }

            return codes;
        }

        private static void CollectGraphErrorCodes(JsonElement element, ISet<string> codes)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String)
                    {
                        var value = codeElement.GetString();
                        if (!string.IsNullOrWhiteSpace(value)) codes.Add(value!);
                    }

                    if (element.TryGetProperty("innerError", out var innerElement))
                        CollectGraphErrorCodes(innerElement, codes);

                    foreach (var property in element.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "code", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(property.Name, "innerError", StringComparison.OrdinalIgnoreCase))
                            continue;

                        CollectGraphErrorCodes(property.Value, codes);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        CollectGraphErrorCodes(item, codes);
                    break;
            }
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

            rawResponse = TryGetExceptionStringProperty(ex, "RawResponseBody")
                ?? TryGetExceptionStringProperty(ex, "ResponseBody")
                ?? TryGetExceptionStringProperty(ex, "ResponseContent");

            if ((string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(message)) && !string.IsNullOrWhiteSpace(rawResponse))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawResponse);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                        doc.RootElement.TryGetProperty("error", out var errorElement) &&
                        errorElement.ValueKind == JsonValueKind.Object)
                    {
                        if (string.IsNullOrWhiteSpace(code) &&
                            errorElement.TryGetProperty("code", out var codeElement) &&
                            codeElement.ValueKind == JsonValueKind.String)
                        {
                            code = codeElement.GetString();
                        }

                        if (string.IsNullOrWhiteSpace(message) &&
                            errorElement.TryGetProperty("message", out var messageElement) &&
                            messageElement.ValueKind == JsonValueKind.String)
                        {
                            message = messageElement.GetString();
                        }
                    }
                }
                catch (JsonException) { }
            }

            return (code, message, rawResponse);
        }

        private static string? TryGetExceptionStringProperty(ServiceException ex, string propertyName)
        {
            var prop = ex.GetType().GetProperty(propertyName);
            if (prop is null) return null;
            if (prop.GetValue(ex) is string value) return value;
            return null;
        }

        private static string? TrySummarizeGraphRawResponse(string rawResponse)
        {
            if (string.IsNullOrWhiteSpace(rawResponse)) return null;

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
                            if (!string.IsNullOrWhiteSpace(baseDetail)) parts.Add(baseDetail!);
                        }
                        if (!string.IsNullOrWhiteSpace(inner)) parts.Add(inner!);

                        if (parts.Count > 0) return string.Join("; ", parts);
                    }
                }
            }
            catch (JsonException) { }

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

            return properties.Count == 0 ? null : "InnerError => " + string.Join(", ", properties);
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
                    candidate = nestedIdEl.GetString();

                if (string.IsNullOrWhiteSpace(candidate) &&
                    el.TryGetProperty("onlineMeetingId", out var nestedMeetingIdEl) &&
                    nestedMeetingIdEl.ValueKind == JsonValueKind.String)
                    candidate = nestedMeetingIdEl.GetString();

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

            if (TryExtractOnlineMeetingId(meeting.OnlineJoinUrl, out id)) return true;
            if (TryExtractOnlineMeetingId(GetJoinUrlFromEvent(graphEvent), out id)) return true;
            if (TryExtractOnlineMeetingId(GetJoinUrlFromOnlineMeeting(meetingResource), out id)) return true;

            return false;
        }

        private static bool TryExtractOnlineMeetingId(string? source, out string? id)
        {
            id = null;
            if (string.IsNullOrWhiteSpace(source)) return false;

            var decoded = WebUtility.HtmlDecode(source);
            var match = TeamsMeetingIdRegex.Match(decoded ?? source);
            if (!match.Success) return false;

            var raw = match.Value;

            try
            {
                var unescaped = Uri.UnescapeDataString(raw);
                if (string.IsNullOrWhiteSpace(unescaped)) return false;

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
                return inlineJoinUrl;

            if (!string.IsNullOrWhiteSpace(graphEvent?.OnlineMeetingUrl))
                return graphEvent!.OnlineMeetingUrl;

            if (graphEvent?.AdditionalData != null)
            {
                if (TryExtractAdditionalString(graphEvent.AdditionalData, "onlineMeetingUrl", out var fromRoot))
                    return fromRoot;

                if (graphEvent.AdditionalData.TryGetValue("onlineMeeting", out var nested) &&
                    TryReadJoinUrlFromAdditionalData(nested, out var nestedJoinUrl))
                    return nestedJoinUrl;
            }

            return null;
        }

        private static string? GetJoinUrlFromOnlineMeeting(OnlineMeeting? meetingResource)
        {
            if (meetingResource is null) return null;

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
                    return joinInfoContent;
            }

            return null;
        }

        private static bool TryExtractAdditionalString(IDictionary<string, object?> data, string key, out string? value)
        {
            value = null;
            if (!data.TryGetValue(key, out var raw) || raw is null) return false;

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
