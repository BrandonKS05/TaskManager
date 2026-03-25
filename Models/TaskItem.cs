namespace TaskManager.Models;

public class TaskItem
{
    public Guid Id { get; init; }
    public Guid ListId { get; init; }
    public string Title { get; init; } = "";
    public bool IsComplete { get; set; }
    public DateTime CreatedAtUtc { get; init; }
    public string? Tag { get; set; }
    /// <summary>1–5 star urgency rating derived from the urgency formula.</summary>
    public int Priority { get; set; }
    public int Importance { get; set; }
    public int Complexity { get; set; }
    /// <summary>Due date as yyyy-MM-dd (calendar date).</summary>
    public string? DueDate { get; set; }
}
