namespace dependencies_visualizer.Services;

public sealed class NodeShapeResolver(NodeTypeCatalog nodeTypeCatalog)
{
    private readonly NodeTypeCatalog _nodeTypeCatalog = nodeTypeCatalog;

    public string Resolve(string nodeType)
    {
        return _nodeTypeCatalog.ResolveShape(nodeType);
    }

    public bool IsCompoundType(string nodeType)
    {
        return _nodeTypeCatalog.IsCompound(nodeType);
    }
}
