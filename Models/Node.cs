using System.ComponentModel.DataAnnotations;

namespace dependencies_visualizer.Models;

public sealed class Node
{
    public int Id { get; init; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty;

    [StringLength(400)]
    public string? Description { get; set; }
}
