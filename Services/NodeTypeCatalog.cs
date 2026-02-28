using dependencies_visualizer.Models;

namespace dependencies_visualizer.Services;

public sealed class NodeTypeCatalog(IConfiguration configuration)
{
    private static readonly NodeTypeDefinition[] DefaultNodeTypes =
    [
        new() { Name = "Module", Shape = "round-rectangle", Kind = NodeTypeKind.Regular },
        new() { Name = "Page", Shape = "rectangle", Kind = NodeTypeKind.Regular },
        new() { Name = "Component", Shape = "hexagon", Kind = NodeTypeKind.Regular },
        new() { Name = "Database", Shape = "barrel", Kind = NodeTypeKind.Regular },
        new() { Name = "StoredProcedure", Shape = "tag", Kind = NodeTypeKind.Regular },
        new() { Name = "Table", Shape = "diamond", Kind = NodeTypeKind.Regular },
        new() { Name = "Server", Shape = "octagon", Kind = NodeTypeKind.Regular },
        new() { Name = "Api", Shape = "triangle", Kind = NodeTypeKind.Regular },
        new() { Name = "Service", Shape = "ellipse", Kind = NodeTypeKind.Regular },
        new() { Name = "Other", Shape = "round-diamond", Kind = NodeTypeKind.Regular }
    ];

    private readonly IConfiguration _configuration = configuration;

    public IReadOnlyList<NodeTypeDefinition> GetDefinitions()
    {
        var configuredDefinitions = _configuration.GetSection("NodeTypes").Get<List<NodeTypeDefinition>>() ?? [];
        if (configuredDefinitions.Count > 0)
        {
            return NormalizeDefinitions(configuredDefinitions);
        }

        var legacyNodeTypeNames = _configuration.GetSection("NodeTypes").Get<string[]>() ?? [];
        if (legacyNodeTypeNames.Length > 0)
        {
            var definitions = legacyNodeTypeNames
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v =>
                {
                    var name = v.Trim();
                    var shape = _configuration[$"NodeShapes:{name}"];
                    return new NodeTypeDefinition
                    {
                        Name = name,
                        Shape = CytoscapeShapeCatalog.IsValid(shape) ? shape!.Trim() : "ellipse",
                        Kind = NodeTypeKind.Regular
                    };
                })
                .ToList();

            return NormalizeDefinitions(definitions);
        }

        return DefaultNodeTypes
            .Select(Clone)
            .ToList();
    }

    public IReadOnlyList<string> GetNodeTypes()
    {
        return GetDefinitions()
            .Select(d => d.Name)
            .ToList();
    }

    public string ResolveShape(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return "ellipse";
        }

        var definition = GetDefinitions()
            .FirstOrDefault(d => string.Equals(d.Name, nodeType.Trim(), StringComparison.OrdinalIgnoreCase));
        return definition?.Shape ?? "ellipse";
    }

    public bool IsCompound(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return false;
        }

        var definition = GetDefinitions()
            .FirstOrDefault(d => string.Equals(d.Name, nodeType.Trim(), StringComparison.OrdinalIgnoreCase));
        return definition?.Kind == NodeTypeKind.Compound;
    }

    public bool IsValid(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return false;
        }

        return GetNodeTypes().Contains(nodeType.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<NodeTypeDefinition> NormalizeDefinitions(IEnumerable<NodeTypeDefinition> definitions)
    {
        var normalized = definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .GroupBy(d => d.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var chosen = g.First();
                var shape = CytoscapeShapeCatalog.IsValid(chosen.Shape) ? chosen.Shape.Trim() : "ellipse";
                return new NodeTypeDefinition
                {
                    Name = g.Key,
                    Shape = shape,
                    Kind = chosen.Kind
                };
            })
            .OrderBy(d => d.Name)
            .ToList();

        return normalized.Count > 0
            ? normalized
            : DefaultNodeTypes.Select(Clone).ToList();
    }

    private static NodeTypeDefinition Clone(NodeTypeDefinition definition)
    {
        return new NodeTypeDefinition
        {
            Name = definition.Name,
            Shape = definition.Shape,
            Kind = definition.Kind
        };
    }
}
