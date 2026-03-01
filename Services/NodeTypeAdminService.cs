using dependencies_visualizer.Models;

namespace dependencies_visualizer.Services;

public sealed class NodeTypeAdminService(
    NodeTypeCatalog nodeTypeCatalog,
    DependencyRepository dependencyRepository)
{
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

        _nodeTypeCatalog.Upsert(new NodeTypeDefinition
        {
            Name = normalizedName,
            Shape = normalizedShape,
            Kind = kind
        });
        return true;
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

        if (_dependencyRepository.IsNodeTypeInUse(normalizedName))
        {
            error = "This node type is used by existing nodes and cannot be deleted.";
            return false;
        }

        if (_nodeTypeCatalog.GetDefinitions().Count <= 1)
        {
            error = "At least one node type must exist.";
            return false;
        }

        var deleted = _nodeTypeCatalog.Delete(normalizedName);
        if (!deleted)
        {
            error = "Node type not found.";
            return false;
        }

        return true;
    }
}
