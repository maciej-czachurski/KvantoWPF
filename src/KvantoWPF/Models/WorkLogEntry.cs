namespace KvantoWPF.Models;

public sealed class WorkLogEntry
{
    public Guid TaskId { get; init; }
    public string TaskTitle { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#EF4444";
    public DateTime StartedAt { get; init; }
    public int Minutes { get; init; }
}
