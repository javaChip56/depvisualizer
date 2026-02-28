using System.Text.Json;
using dependencies_visualizer.Models;

namespace dependencies_visualizer.Services;

public sealed class NodeTypeAdminService(
    IWebHostEnvironment environment,
    NodeTypeCatalog nodeTypeCatalog,
    DependencyRepository dependencyRepository)
{
    private readonly object _syncRoot = new();
    private readonly string _nodeTypesPath = Path.Combine(environment.ContentRootPath, "node-types.json");
    private readonly string _nodeShapesPath = Path.Combine(environment.ContentRootPath, "node-shapes.json");
    private readonly NodeTypeCatalog _nodeTypeCatalog = nodeTypeCatalog;
    private readonly DependencyRepository _dependencyRepository = dependencyRepository;

    public IReadOnlyList<NodeTypeDefinition> GetDefinitions()
    {
        return _nodeTypeCatalog.GetDefinitions();
    }

    public bool Upsert(string name, string shape, NodeTypeKind kind, out string? error)
    {
        error = null;
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedShape = shape?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            error = "Node type name is required.";
            return false;
        }

        if (!CytoscapeShapeCatalog.IsValid(normalizedShape))
        {
            error = "Select a valid Cytoscape shape.";
            return false;
        }

        lock (_syncRoot)
        {
            var definitions = _nodeTypeCatalog.GetDefinitions()
                .ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
            definitions[normalizedName] = new NodeTypeDefinition
            {
                Name = normalizedName,
                Shape = normalizedShape,
                Kind = kind
            };

            SaveDefinitions(definitions.Values);
            return true;
        }
    }

    public bool Delete(string name, out string? error)
    {
        error = null;
        var normalizedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            error = "Node type name is required.";
            return false;
        }

        lock (_syncRoot)
        {
            if (_dependencyRepository.IsNodeTypeInUse(normalizedName))
            {
                error = "This node type is used by existing nodes and cannot be deleted.";
                return false;
            }

            var definitions = _nodeTypeCatalog.GetDefinitions()
                .ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
            if (!definitions.Remove(normalizedName))
            {
                error = "Node type not found.";
                return false;
            }

            if (definitions.Count == 0)
            {
                error = "At least one node type must exist.";
                return false;
            }

            SaveDefinitions(definitions.Values);
            return true;
        }
    }

    private void SaveDefinitions(IEnumerable<NodeTypeDefinition> definitions)
    {
        var normalizedDefinitions = definitions
            .Select(d => new NodeTypeDefinition
            {
                Name = d.Name.Trim(),
                Shape = d.Shape.Trim(),
                Kind = d.Kind
            })
            .OrderBy(d => d.Name)
            .ToList();

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var typesPayload = JsonSerializer.Serialize(new { NodeTypes = normalizedDefinitions }, jsonOptions);
        File.WriteAllText(_nodeTypesPath, typesPayload);

        var shapesPayload = JsonSerializer.Serialize(
            new
            {
                NodeShapes = normalizedDefinitions.ToDictionary(d => d.Name, d => d.Shape)
            },
            jsonOptions);
        File.WriteAllText(_nodeShapesPath, shapesPayload);
    }
}
