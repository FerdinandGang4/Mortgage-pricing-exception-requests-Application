using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PricingExceptions.API.Exceptions;

namespace PricingExceptions.API.Middleware;

public sealed class GlobalExceptionHandler(IProblemDetailsService problems, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (404, "Resource not found"),
            ConflictException => (409, "Conflicting operation"),
            BusinessRuleException => (400, "Business rule validation failed"),
            ForbiddenException => (403, "Operation forbidden"),
            _ => (500, "An unexpected error occurred")
        };
        context.Response.StatusCode = status;
        if (status == 500)
            logger.LogError(exception, "Unhandled API error. Trace ID: {TraceId}", context.TraceIdentifier);
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = status, Title = title,
                Detail = status == 500 ? null : exception.Message,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }
        });
        return true;
    }
}
