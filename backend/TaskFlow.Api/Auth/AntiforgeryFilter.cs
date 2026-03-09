using Microsoft.AspNetCore.Antiforgery;

namespace TaskFlow.Api.Auth;

internal sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(statusCode: 400, title: "Missing or invalid CSRF token.");
        }

        return await next(context);
    }
}
