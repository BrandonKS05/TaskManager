using TaskManager.Models;
using TaskManager.Services;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(port))
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddSingleton<TaskStore>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var lists = app.MapGroup("/api/lists");

lists.MapGet("/", (TaskStore store) => Results.Ok(store.GetLists()));

lists.MapPost("/", (TaskStore store, CreateListRequest body) =>
{
    var list = store.AddList(body.Name);
    return list is null
        ? Results.BadRequest(new { error = "Name is required." })
        : Results.Created($"/api/lists/{list.Id}", list);
});

lists.MapDelete("/{id:guid}", (Guid id, TaskStore store) =>
    store.TryDeleteList(id) switch
    {
        DeleteListResult.Deleted => Results.NoContent(),
        DeleteListResult.LastList => Results.BadRequest(new { error = "Cannot delete the last list." }),
        _ => Results.NotFound()
    });

lists.MapGet("/{listId:guid}/tasks", (Guid listId, TaskStore store) =>
    Results.Ok(store.GetTasks(listId)));

lists.MapPost("/{listId:guid}/tasks", (Guid listId, TaskStore store, CreateTaskRequest body) =>
{
    var task = store.AddTask(
        listId,
        body.Title,
        body.Tag,
        body.Importance ?? 3,
        body.Complexity ?? 3,
        body.DueDate);
    return task is null
        ? Results.BadRequest(new { error = "Invalid list or title." })
        : Results.Created($"/api/tasks/{task.Id}", task);
});

var tasks = app.MapGroup("/api/tasks");

tasks.MapPatch("/{id:guid}/complete", (Guid id, TaskStore store) =>
    store.Complete(id) ? Results.NoContent() : Results.NotFound());

tasks.MapDelete("/{id:guid}", (Guid id, TaskStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();

internal record CreateListRequest(string? Name);

internal record CreateTaskRequest(string? Title, string? Tag, int? Importance, int? Complexity, string? DueDate);
