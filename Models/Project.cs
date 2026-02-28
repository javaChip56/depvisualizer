using System.ComponentModel.DataAnnotations;

namespace dependencies_visualizer.Models;

public sealed class Project
{
    public int Id { get; init; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(600)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string OwnerUsername { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
}
