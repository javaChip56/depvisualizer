namespace dependencies_visualizer.Services;

public sealed class NodeTypeCatalog(IConfiguration configuration)
{
    private static readonly string[] DefaultNodeTypes =
    [
        "Module",
        "Page",
        "Component",
        "Database",
        "StoredProcedure",
        "Table",
        "Server",
        "Api",
        "Service",
        "Other"
    ];

    private readonly IConfiguration _configuration = configuration;

    public IReadOnlyList<string> GetNodeTypes()
    {
        var configured = _configuration.GetSection("NodeTypes").Get<string[]>() ?? [];

        var values = configured
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values.Count > 0 ? values : DefaultNodeTypes;
    }

    public bool IsValid(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
        {
            return false;
        }

        return GetNodeTypes().Contains(nodeType.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
