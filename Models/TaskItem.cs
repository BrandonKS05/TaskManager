namespace TaskManager.Models;

public class TaskItem
{
    public Guid Id { get; init; }
    public Guid ListId { get; init; }
    public string Title { get; init; } = "";
    public bool IsComplete { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public string? Tag { get; set; }
    public int Priority { get; set; }
}
