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
    private readonly List<TodoList> _lists = [];
    private readonly List<TaskItem> _tasks = [];
    private readonly object _lock = new();

    public TaskStore()
    {
        _lists.Add(new TodoList { Id = Guid.NewGuid(), Name = "Life" });
        _lists.Add(new TodoList { Id = Guid.NewGuid(), Name = "Work" });
    }

    public IReadOnlyList<TodoList> GetLists()
    {
        lock (_lock)
            return [.. _lists];
    }

    public TodoList? AddList(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var list = new TodoList
        {
            Id = Guid.NewGuid(),
            Name = name.Trim()
        };

        lock (_lock)
            _lists.Add(list);

        return list;
    }

    public DeleteListResult TryDeleteList(Guid id)
    {
        lock (_lock)
        {
            var index = _lists.FindIndex(l => l.Id == id);
            if (index < 0)
                return DeleteListResult.NotFound;

            if (_lists.Count <= 1)
                return DeleteListResult.LastList;

            _tasks.RemoveAll(t => t.ListId == id);
            _lists.RemoveAt(index);
            return DeleteListResult.Deleted;
        }
    }

    public IReadOnlyList<TaskItem> GetTasks(Guid listId)
    {
        lock (_lock)
        {
            if (_lists.All(l => l.Id != listId))
                return [];

            return [.. _tasks.Where(t => t.ListId == listId)];
        }
    }

    public TaskItem? AddTask(Guid listId, string? title, string? tag, int importance, int complexity, string? dueDateYyyyMmDd)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        importance = Math.Clamp(importance, 1, 5);
        complexity = Math.Clamp(complexity, 1, 5);
        var now = DateTime.UtcNow;
        var daysRemaining = UrgencyCalculator.DaysRemainingFromDueDate(dueDateYyyyMmDd, now);
        var stars = UrgencyCalculator.ComputeStars(importance, complexity, daysRemaining);
        var dueNorm = NormalizeDueDate(dueDateYyyyMmDd);

        lock (_lock)
        {
            if (_lists.All(l => l.Id != listId))
                return null;

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
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

            _tasks.Add(task);
            return task;
        }
    }

    private static string? NormalizeDueDate(string? due)
    {
        if (string.IsNullOrWhiteSpace(due))
            return null;
        return DateOnly.TryParse(due.Trim(), out var d) ? d.ToString("yyyy-MM-dd") : null;
    }

    public bool Complete(Guid id)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task is null)
                return false;
            task.IsComplete = true;
            return true;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_lock)
        {
            var index = _tasks.FindIndex(t => t.Id == id);
            if (index < 0)
                return false;
            _tasks.RemoveAt(index);
            return true;
        }
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
