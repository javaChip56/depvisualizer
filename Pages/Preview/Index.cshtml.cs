using System.Text.Json;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dependencies_visualizer.Pages.Preview;

[IgnoreAntiforgeryToken]
public sealed class IndexModel(DependencyRepository repository, NodeShapeResolver nodeShapeResolver) : PageModel
{
    private readonly DependencyRepository _repository = repository;
    private readonly NodeShapeResolver _nodeShapeResolver = nodeShapeResolver;

    public string CytoscapeElementsJson { get; private set; } = "[]";

    public void OnGet()
    {
        var nodes = _repository.GetNodes();
        var relationships = _repository.GetRelationships();
        var nodeIds = nodes.Select(n => $"n{n.Id}").ToList();
        var savedPositions = _repository.GetLayoutPositions(nodeIds);

        var elements = new List<object>();

        foreach (var node in nodes)
        {
            var nodeId = $"n{node.Id}";
            var data = new Dictionary<string, object>
            {
                ["id"] = nodeId,
                ["label"] = node.Name,
                ["type"] = node.Type,
                ["shape"] = _nodeShapeResolver.Resolve(node.Type)
            };

            if (savedPositions.TryGetValue(nodeId, out var savedPosition))
            {
                elements.Add(new
                {
                    data,
                    position = new { x = savedPosition.X, y = savedPosition.Y }
                });
            }
            else
            {
                elements.Add(new { data });
            }
        }

        foreach (var relationship in relationships)
        {
            elements.Add(new
            {
                data = new Dictionary<string, object>
                {
                    ["id"] = $"e{relationship.Id}",
                    ["source"] = $"n{relationship.SourceNodeId}",
                    ["target"] = $"n{relationship.TargetNodeId}",
                    ["label"] = relationship.Label
                }
            });
        }

        CytoscapeElementsJson = JsonSerializer.Serialize(elements);
    }

    public async Task<IActionResult> OnPostSaveLayoutAsync()
    {
        var postedPositions = await JsonSerializer.DeserializeAsync<Dictionary<string, NodeLayoutPosition>>(
            Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var validNodeIds = _repository
            .GetNodes()
            .Select(n => $"n{n.Id}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filtered = postedPositions
            .Where(p =>
                validNodeIds.Contains(p.Key) &&
                double.IsFinite(p.Value.X) &&
                double.IsFinite(p.Value.Y))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        _repository.SaveLayoutPositions(filtered);

        return new JsonResult(new { success = true, saved = filtered.Count });
    }
}
