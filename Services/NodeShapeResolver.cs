namespace dependencies_visualizer.Services;

public sealed class NodeShapeResolver(IConfiguration configuration)
{
    private static readonly HashSet<string> ValidShapes = new(StringComparer.OrdinalIgnoreCase)
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

    private readonly IConfiguration _configuration = configuration;

    public string Resolve(string nodeType)
    {
        var configuredShape = _configuration[$"NodeShapes:{nodeType.Trim()}"];
        if (!string.IsNullOrWhiteSpace(configuredShape) && ValidShapes.Contains(configuredShape))
        {
            return configuredShape;
        }

        return "ellipse";
    }
}
