using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BoardMgmt.WebApi.Common.Http;

public class InvalidModelStateFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(kv => kv.Value?.Errors.Count > 0)
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );

            var resp = ApiErrorResponse.From(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "One or more validation errors occurred.",
                errors,
                context.HttpContext.TraceIdentifier
            );

            context.Result = new JsonResult(resp) { StatusCode = resp.StatusCode };
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
