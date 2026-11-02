using DeviceManagerMVC.Application.Common;
using DeviceManagerMVC.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DeviceManagerMVC.Api.ErrorHandling;

/// <summary>
/// Translates known exceptions into ProblemDetails. Anything else is left unhandled
/// so the default handler answers with a generic 500 that leaks no details.
/// </summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessRuleViolationException => (StatusCodes.Status409Conflict, "Business rule violated"),
            _ => (0, null)
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        // Messages of these exceptions are written for API consumers, so they are safe to expose.
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exception.Message
            }
        });
    }
}
