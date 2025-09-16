using System.Text.Json.Serialization;

namespace BoardMgmt.WebApi.Common.Http;

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    string TraceId,
    DateTimeOffset Timestamp
)
{
    public static ApiResponse<T> Ok(T data, string? message, string traceId) =>
        new(true, data, message, traceId, DateTimeOffset.UtcNow);
}

public record ApiError(
    string Code,
    string Message,
    object? Details // e.g. validation dictionary, extra info, etc.
);

public record ApiErrorResponse(
    bool Success,
    ApiError Error,
    string TraceId,
    DateTimeOffset Timestamp
)
{
    [JsonIgnore] public int StatusCode { get; init; } = StatusCodes.Status500InternalServerError;

    public static ApiErrorResponse From(int status, string code, string message, object? details, string traceId) =>
        new(false, new ApiError(code, message, details), traceId, DateTimeOffset.UtcNow) { StatusCode = status };
}
