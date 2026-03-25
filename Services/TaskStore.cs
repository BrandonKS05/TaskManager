using System.Data;
using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Services;

public enum DeleteListResult
{
    NotFound,
    LastList,
    Deleted
}

public class TaskStore
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public TaskStore(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private string DeviceId
    {
        get
        {
            var id = _http.HttpContext?.Items["DeviceId"] as string;
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("device-id is required for TaskStore.");
            return id.Trim();
        }
    }

    public async Task<IReadOnlyList<TodoList>> GetListsAsync(CancellationToken cancellationToken = default)
    {
        var deviceId = DeviceId;
        await EnsureDefaultListsAsync(deviceId, cancellationToken);
        return await _db.Lists
            .AsNoTracking()
            .Where(l => l.DeviceId == deviceId)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);
    }

    private static async Task EnsureDefaultListsAsync(AppDbContext db, string deviceId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (await db.Lists.AnyAsync(l => l.DeviceId == deviceId, ct))
            {
                await tx.CommitAsync(ct);
                return;
            }

            db.Lists.AddRange(
                new TodoList { Id = Guid.NewGuid(), DeviceId = deviceId, Name = "Life" },
                new TodoList { Id = Guid.NewGuid(), DeviceId = deviceId, Name = "Work" });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private Task EnsureDefaultListsAsync(string deviceId, CancellationToken ct)
        => EnsureDefaultListsAsync(_db, deviceId, ct);

    public async Task<TodoList?> AddListAsync(string? name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var deviceId = DeviceId;
        var list = new TodoList
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            Name = name.Trim()
        };

        _db.Lists.Add(list);
        await _db.SaveChangesAsync(cancellationToken);
        return list;
    }

    public async Task<DeleteListResult> TryDeleteListAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deviceId = DeviceId;

        var list = await _db.Lists.FirstOrDefaultAsync(l => l.Id == id && l.DeviceId == deviceId, cancellationToken);
        if (list is null)
            return DeleteListResult.NotFound;

        var count = await _db.Lists.CountAsync(l => l.DeviceId == deviceId, cancellationToken);
        if (count <= 1)
            return DeleteListResult.LastList;

        _db.Lists.Remove(list);
        await _db.SaveChangesAsync(cancellationToken);
        return DeleteListResult.Deleted;
    }

    public async Task<IReadOnlyList<TaskItem>> GetTasksAsync(Guid listId, CancellationToken cancellationToken = default)
    {
        var deviceId = DeviceId;

        var listOk = await _db.Lists.AnyAsync(l => l.Id == listId && l.DeviceId == deviceId, cancellationToken);
        if (!listOk)
            return [];

        return await _db.Tasks
            .AsNoTracking()
            .Where(t => t.ListId == listId && t.DeviceId == deviceId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskItem?> AddTaskAsync(
        Guid listId,
        string? title,
        string? tag,
        int importance,
        int complexity,
        string? dueDateYyyyMmDd,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        importance = Math.Clamp(importance, 1, 5);
        complexity = Math.Clamp(complexity, 1, 5);
        var now = DateTime.UtcNow;
        var daysRemaining = UrgencyCalculator.DaysRemainingFromDueDate(dueDateYyyyMmDd, now);
        var stars = UrgencyCalculator.ComputeStars(importance, complexity, daysRemaining);
        var dueNorm = NormalizeDueDate(dueDateYyyyMmDd);
        var deviceId = DeviceId;

        var listOk = await _db.Lists.AnyAsync(l => l.Id == listId && l.DeviceId == deviceId, cancellationToken);
        if (!listOk)
            return null;

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            ListId = listId,
            Title = title.Trim(),
            IsComplete = false,
            CreatedAtUtc = now,
            Tag = NormalizeTag(tag),
            Priority = stars,
            Importance = importance,
            Complexity = complexity,
            DueDate = dueNorm
        };

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<bool> CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deviceId = DeviceId;
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.DeviceId == deviceId, cancellationToken);
        if (task is null)
            return false;

        task.IsComplete = true;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deviceId = DeviceId;
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.DeviceId == deviceId, cancellationToken);
        if (task is null)
            return false;

        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? NormalizeDueDate(string? due)
    {
        if (string.IsNullOrWhiteSpace(due))
            return null;
        return DateOnly.TryParse(due.Trim(), out var d) ? d.ToString("yyyy-MM-dd") : null;
    }

    private static string? NormalizeTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;

        var t = tag.Trim();
        if (t.StartsWith('#'))
            t = t[1..];

        return t.Length == 0 ? null : t;
    }
}
