namespace dependencies_visualizer.Models;

public sealed class DependencyRelationship
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public int SubProjectId { get; init; }
    public int SourceNodeId { get; init; }
    public int TargetNodeId { get; init; }
    public RelationshipArrowDirection ArrowDirection { get; set; } = RelationshipArrowDirection.Down;
    public RelationshipLineStyle LineStyle { get; set; } = RelationshipLineStyle.Solid;
    public string Label { get; set; } = "depends on";
}

public enum RelationshipArrowDirection
{
    Down,
    Up,
    Both
}

public enum RelationshipLineStyle
{
    Solid,
    Dotted,
    Dashed
}
