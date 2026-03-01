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

    private readonly object _syncRoot = new();
    private readonly List<NodeTypeDefinition> _definitions = LoadSeedDefinitions(configuration);

    public IReadOnlyList<NodeTypeDefinition> GetDefinitions()
    {
        lock (_syncRoot)
        {
            return _definitions
                .Select(Clone)
                .OrderBy(d => d.Name)
                .ToList();
        }
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

    public void Upsert(NodeTypeDefinition definition)
    {
        var normalizedName = definition.Name.Trim();
        var normalizedShape = definition.Shape.Trim();
        lock (_syncRoot)
        {
            var existing = _definitions.FirstOrDefault(d =>
                string.Equals(d.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                _definitions.Add(new NodeTypeDefinition
                {
                    Name = normalizedName,
                    Shape = normalizedShape,
                    Kind = definition.Kind
                });
                return;
            }

            existing.Name = normalizedName;
            existing.Shape = normalizedShape;
            existing.Kind = definition.Kind;
        }
    }

    public bool Delete(string nodeTypeName)
    {
        var normalizedName = nodeTypeName.Trim();
        lock (_syncRoot)
        {
            return _definitions.RemoveAll(d =>
                string.Equals(d.Name, normalizedName, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    private static List<NodeTypeDefinition> LoadSeedDefinitions(IConfiguration configuration)
    {
        var configuredDefinitions = configuration.GetSection("NodeTypes").Get<List<NodeTypeDefinition>>() ?? [];
        if (configuredDefinitions.Count > 0)
        {
            return NormalizeDefinitions(configuredDefinitions).ToList();
        }

        var legacyNodeTypeNames = configuration.GetSection("NodeTypes").Get<string[]>() ?? [];
        if (legacyNodeTypeNames.Length > 0)
        {
            var definitions = legacyNodeTypeNames
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v =>
                {
                    var name = v.Trim();
                    var shape = configuration[$"NodeShapes:{name}"];
                    return new NodeTypeDefinition
                    {
                        Name = name,
                        Shape = CytoscapeShapeCatalog.IsValid(shape) ? shape!.Trim() : "ellipse",
                        Kind = NodeTypeKind.Regular
                    };
                })
                .ToList();

            return NormalizeDefinitions(definitions).ToList();
        }

        return DefaultNodeTypes
            .Select(Clone)
            .ToList();
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
