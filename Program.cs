using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;
using TaskManager.Services;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(port))
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TaskStore>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        if (!context.Request.Headers.TryGetValue("device-id", out var deviceId)
            || string.IsNullOrWhiteSpace(deviceId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "device-id header is required." });
            return;
        }

        context.Items["DeviceId"] = deviceId.ToString().Trim();
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

var lists = app.MapGroup("/api/lists");

lists.MapGet("/", async (TaskStore store, CancellationToken ct) =>
    Results.Ok(await store.GetListsAsync(ct)));

lists.MapPost("/", async (TaskStore store, CreateListRequest body, CancellationToken ct) =>
{
    var list = await store.AddListAsync(body.Name, ct);
    return list is null
        ? Results.BadRequest(new { error = "Name is required." })
        : Results.Created($"/api/lists/{list.Id}", list);
});

lists.MapDelete("/{id:guid}", async (Guid id, TaskStore store, CancellationToken ct) =>
    (await store.TryDeleteListAsync(id, ct)) switch
    {
        DeleteListResult.Deleted => Results.NoContent(),
        DeleteListResult.LastList => Results.BadRequest(new { error = "Cannot delete the last list." }),
        _ => Results.NotFound()
    });

lists.MapGet("/{listId:guid}/tasks", async (Guid listId, TaskStore store, CancellationToken ct) =>
    Results.Ok(await store.GetTasksAsync(listId, ct)));

lists.MapPost("/{listId:guid}/tasks", async (Guid listId, TaskStore store, CreateTaskRequest body, CancellationToken ct) =>
{
    var task = await store.AddTaskAsync(
        listId,
        body.Title,
        body.Tag,
        body.Importance ?? 3,
        body.Complexity ?? 3,
        body.DueDate,
        ct);
    return task is null
        ? Results.BadRequest(new { error = "Invalid list or title." })
        : Results.Created($"/api/tasks/{task.Id}", task);
});

var tasks = app.MapGroup("/api/tasks");

tasks.MapPatch("/{id:guid}/complete", async (Guid id, TaskStore store, CancellationToken ct) =>
    await store.CompleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

tasks.MapDelete("/{id:guid}", async (Guid id, TaskStore store, CancellationToken ct) =>
    await store.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();

internal record CreateListRequest(string? Name);

internal record CreateTaskRequest(string? Title, string? Tag, int? Importance, int? Complexity, string? DueDate);
