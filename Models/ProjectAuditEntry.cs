namespace dependencies_visualizer.Models;

public sealed class ProjectAuditEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public string Username { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;
}
