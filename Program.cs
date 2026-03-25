using Microsoft.EntityFrameworkCore;
using Npgsql;
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

var rawConnectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(rawConnectionString))
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

var connectionString = NormalizePostgresConnectionString(rawConnectionString);

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

static string NormalizePostgresConnectionString(string raw)
{
    if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return raw;
    }

    var uri = new Uri(raw);
    var userInfoParts = uri.UserInfo.Split(':', 2);
    var username = userInfoParts.Length > 0 ? Uri.UnescapeDataString(userInfoParts[0]) : "";
    var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : "";
    var database = uri.AbsolutePath.Trim('/'); // Neon-style URL usually has "/dbname"

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.IsDefaultPort ? 5432 : uri.Port,
        Database = database,
        Username = username,
        Password = password
    };

    if (!string.IsNullOrWhiteSpace(uri.Query))
    {
        var pairs = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]).Trim().ToLowerInvariant();
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]).Trim() : "";

            if (key == "sslmode" && Enum.TryParse<SslMode>(value, true, out var sslMode))
                builder.SslMode = sslMode;
            else if (key == "sslmode" && string.Equals(value, "require", StringComparison.OrdinalIgnoreCase))
                builder.SslMode = SslMode.Require;
        }
    }

    if (builder.SslMode == SslMode.Disable)
        builder.SslMode = SslMode.Require;

    return builder.ConnectionString;
}

internal record CreateListRequest(string? Name);

internal record CreateTaskRequest(string? Title, string? Tag, int? Importance, int? Complexity, string? DueDate);
