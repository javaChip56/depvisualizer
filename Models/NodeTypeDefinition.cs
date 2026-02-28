using System.ComponentModel.DataAnnotations;

namespace dependencies_visualizer.Models;

public sealed class NodeTypeDefinition
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Shape { get; set; } = "ellipse";

    public NodeTypeKind Kind { get; set; } = NodeTypeKind.Regular;
}
