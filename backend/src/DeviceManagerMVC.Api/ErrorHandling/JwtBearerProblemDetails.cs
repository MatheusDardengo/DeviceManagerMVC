using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace DeviceManagerMVC.Api.ErrorHandling;

/// <summary>
/// Replaces JWT bearer's empty 401 with a ProblemDetails body, so every error the API returns has the same shape.
/// </summary>
internal static class JwtBearerProblemDetails
{
    public static async Task WriteChallengeAsync(JwtBearerChallengeContext context)
    {
        // Skip the default challenge (bare 401); we write the response ourselves.
        context.HandleResponse();

        var response = context.Response;
        response.StatusCode = StatusCodes.Status401Unauthorized;
        response.Headers[HeaderNames.WWWAuthenticate] = JwtBearerDefaults.AuthenticationScheme;

        var problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                // Deliberately generic: no hint whether the token was missing, expired or forged.
                Detail = "A valid bearer access token is required."
            }
        });
    }
}
