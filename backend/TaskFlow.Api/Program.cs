using TaskFlow.Api.Development;
using TaskFlow.Infrastructure;

var seedDemo = args is ["--seed-demo"];
var builder = WebApplication.CreateBuilder(seedDemo ? [] : args);
builder.Services.AddTaskFlowPersistence(builder.Configuration);
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

app.Run();
