using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TaskFlow.Application.Auth;

namespace TaskFlow.Api.Auth;

internal static class AuthEndpoints
{
    public static void MapTaskFlowAuth(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth");
        auth.AddEndpointFilter(async (request, next) =>
        {
            request.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(request);
        });

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery, IHostEnvironment environment) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                Secure = !environment.IsDevelopment() || context.Request.IsHttps,
                Path = "/"
            });
            return Results.NoContent();
        });

        auth.MapPost("/login", LoginAsync).AddEndpointFilter<AntiforgeryFilter>();
        auth.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>();
        auth.MapGet("/session", (ClaimsPrincipal principal) => Results.Ok(new SignedInUser(
            int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
            principal.FindFirstValue(ClaimTypes.Email)!,
            principal.Identity!.Name!))).RequireAuthorization();
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, HttpContext context, SignInService signIn, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > 254)
        {
            errors["email"] = ["Enter an email address of at most 254 characters."];
        }
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
        {
            errors["password"] = ["Enter a password of at most 1024 characters."];
        }
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var user = await signIn.VerifyAsync(request.Email!, request.Password!, cancellationToken);
        if (user is null)
        {
            return Results.Problem(statusCode: 401, title: "Invalid email or password.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(new ClaimsPrincipal(identity));
        return Results.Ok(user);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync();
        return Results.NoContent();
    }

    private sealed record LoginRequest(string? Email, string? Password);
}
