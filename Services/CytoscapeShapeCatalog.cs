namespace dependencies_visualizer.Services;

public static class CytoscapeShapeCatalog
{
    private static readonly HashSet<string> ValidShapesSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "ellipse",
        "triangle",
        "round-triangle",
        "rectangle",
        "round-rectangle",
        "cut-rectangle",
        "bottom-round-rectangle",
        "diamond",
        "round-diamond",
        "pentagon",
        "round-pentagon",
        "hexagon",
        "round-hexagon",
        "concave-hexagon",
        "heptagon",
        "round-heptagon",
        "octagon",
        "round-octagon",
        "star",
        "tag",
        "round-tag",
        "barrel",
        "vee",
        "rhomboid",
        "right-rhomboid"
    };

    public static IReadOnlyList<string> ValidShapes { get; } = ValidShapesSet
        .OrderBy(s => s)
        .ToList();

    public static bool IsValid(string? shape)
    {
        return !string.IsNullOrWhiteSpace(shape) && ValidShapesSet.Contains(shape.Trim());
    }
}
