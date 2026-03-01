using System.ComponentModel.DataAnnotations;

namespace dependencies_visualizer.Models;

public sealed class SubProject
{
    public int Id { get; init; }
    public int ProjectId { get; init; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Description { get; set; }

    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
}
