using System.Text.Json.Serialization;

namespace TaskManager.Models;

public class TaskItem
{
    public Guid Id { get; set; }

    [JsonIgnore]
    public string DeviceId { get; set; } = "";

    public Guid ListId { get; set; }
    public string Title { get; set; } = "";
    public bool IsComplete { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Tag { get; set; }
    /// <summary>Manual priority / stars (1–5).</summary>
    public int Priority { get; set; }
    /// <summary>Due date as yyyy-MM-dd (calendar date).</summary>
    public string? DueDate { get; set; }
}
