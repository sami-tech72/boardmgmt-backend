using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Common.Http;

public static class ControllerBaseExtensions
{
    public static IActionResult OkApi<T>(this ControllerBase c, T data, string? message = null)
        => c.Ok(ApiResponse<T>.Ok(data, message, c.HttpContext.TraceIdentifier));

    public static IActionResult CreatedApi<T>(this ControllerBase c, string locationAction, object? routeValues, T data, string? message = null)
        => c.CreatedAtAction(locationAction, routeValues, ApiResponse<T>.Ok(data, message, c.HttpContext.TraceIdentifier));

    // NEW: uniform 401 body
    public static IActionResult UnauthorizedApi(this ControllerBase c, string code, string message, object? details = null)
        => c.Unauthorized(ApiErrorResponse.From(
            StatusCodes.Status401Unauthorized, code, message, details, c.HttpContext.TraceIdentifier));
}
