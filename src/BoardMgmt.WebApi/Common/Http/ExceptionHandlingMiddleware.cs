using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.WebApi.Common.Http;

public class ExceptionHandlingMiddleware : IMiddleware
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, code, message, details) = MapException(ex);
            var body = ApiErrorResponse.From(status, code, message, details, context.TraceIdentifier);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = body.StatusCode;
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, _json));
        }
    }

    private static (int status, string code, string message, object? details) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException vex => (
                StatusCodes.Status400BadRequest,
                "validation_error",
                "One or more validation errors occurred.",
                vex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            ),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Authentication is required to access this resource.",
                null
            ),

            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "not_found",
                "The requested resource was not found.",
                null
            ),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "concurrency_conflict",
                "The resource was updated by another process.",
                null
            ),

            DbUpdateException dbex => (
                StatusCodes.Status409Conflict,
                "db_update_error",
                "A database error occurred while saving your changes.",
                new { dbex.InnerException?.Message }
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                "server_error",
                "An unexpected error occurred. Please try again.",
                new { ex.Message }
            )
        };
    }
}
