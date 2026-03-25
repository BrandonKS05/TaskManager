using System.Text.Json.Serialization;

namespace TaskManager.Models;

public class TodoList
{
    public Guid Id { get; set; }

    [JsonIgnore]
    public string DeviceId { get; set; } = "";

    public string Name { get; set; } = "";
}
