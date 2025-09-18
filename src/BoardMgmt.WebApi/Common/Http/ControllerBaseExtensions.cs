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




    

   

    // ❗ 400 uniform body (needed by your AuthController)
    public static IActionResult BadRequestApi(this ControllerBase c, string code, string message, object? details = null)
        => c.BadRequest(ApiErrorResponse.From(
            StatusCodes.Status400BadRequest, code, message, details, c.HttpContext.TraceIdentifier));

    // (optional) 403
    public static IActionResult ForbidApi(this ControllerBase c, string code, string message, object? details = null)
        => new ObjectResult(ApiErrorResponse.From(
            StatusCodes.Status403Forbidden, code, message, details, c.HttpContext.TraceIdentifier))
        { StatusCode = StatusCodes.Status403Forbidden };

    // (optional) 404
    public static IActionResult NotFoundApi(this ControllerBase c, string code, string message, object? details = null)
        => c.NotFound(ApiErrorResponse.From(
            StatusCodes.Status404NotFound, code, message, details, c.HttpContext.TraceIdentifier));
}
