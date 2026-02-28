namespace dependencies_visualizer.Models;

public sealed class DependencyRelationship
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public int SourceNodeId { get; init; }
    public int TargetNodeId { get; init; }

    public string Label => "depends on";
}
