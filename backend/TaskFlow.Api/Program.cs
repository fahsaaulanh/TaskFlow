using Microsoft.AspNetCore.Authentication.Cookies;
using System.Text.Json.Serialization;
using TaskFlow.Api.Auth;
using TaskFlow.Api.Boards;
using TaskFlow.Api.Development;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Boards;
using TaskFlow.Infrastructure;

var seedDemo = args is ["--seed-demo"];
var builder = WebApplication.CreateBuilder(seedDemo ? [] : args);
builder.Services.AddTaskFlowPersistence(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddScoped<SignInService>();
builder.Services.AddScoped<TaskWriter>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordVerifier, IdentityPasswordVerifier>();
var cookiePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "TaskFlow.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookiePolicy;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.SecurePolicy = cookiePolicy;
});
var app = builder.Build();

if (seedDemo)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Demo data can only be seeded in Development.");
    }

    await DemoDataSeeder.SeedAsync(app.Services, app.Configuration);
    return;
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapTaskFlowAuth();
app.MapTaskFlowBoards();

app.Run();
