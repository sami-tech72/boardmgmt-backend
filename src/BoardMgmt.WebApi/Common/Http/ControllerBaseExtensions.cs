using Microsoft.AspNetCore.Mvc;

namespace BoardMgmt.WebApi.Common.Http;

public static class ControllerBaseExtensions
{
    public static IActionResult OkApi<T>(this ControllerBase c, T data, string? message = null)
        => c.Ok(ApiResponse<T>.Ok(data, message, c.HttpContext.TraceIdentifier));

    public static IActionResult CreatedApi<T>(
        this ControllerBase c,
        string locationAction,
        object? routeValues,
        T data,
        string? message = null)
        => c.CreatedAtAction(locationAction, routeValues,
            ApiResponse<T>.Ok(data, message, c.HttpContext.TraceIdentifier));

    // 204 with envelope (handy for deletes)
    public static IActionResult NoContentApi(this ControllerBase c, string? message = null)
        => new ObjectResult(ApiResponse<object>.Ok(null, message, c.HttpContext.TraceIdentifier))
        { StatusCode = StatusCodes.Status204NoContent };

    // 400
    public static IActionResult BadRequestApi(this ControllerBase c, string code, string message, object? details = null)
        => c.BadRequest(ApiErrorResponse.From(
            StatusCodes.Status400BadRequest, code, message, details, c.HttpContext.TraceIdentifier));

    // 401
    public static IActionResult UnauthorizedApi(this ControllerBase c, string code, string message, object? details = null)
        => c.Unauthorized(ApiErrorResponse.From(
            StatusCodes.Status401Unauthorized, code, message, details, c.HttpContext.TraceIdentifier));

    // 403
    public static IActionResult ForbidApi(this ControllerBase c, string code, string message, object? details = null)
        => new ObjectResult(ApiErrorResponse.From(
            StatusCodes.Status403Forbidden, code, message, details, c.HttpContext.TraceIdentifier))
        { StatusCode = StatusCodes.Status403Forbidden };

    // 404
    public static IActionResult NotFoundApi(this ControllerBase c, string code, string message, object? details = null)
        => c.NotFound(ApiErrorResponse.From(
            StatusCodes.Status404NotFound, code, message, details, c.HttpContext.TraceIdentifier));
}
