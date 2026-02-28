using System.Text.Json;
using dependencies_visualizer.Models;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dependencies_visualizer.Pages.Preview;

[IgnoreAntiforgeryToken]
public sealed class IndexModel(DependencyRepository repository, NodeShapeResolver nodeShapeResolver) : PageModel
{
    private readonly DependencyRepository _repository = repository;
    private readonly NodeShapeResolver _nodeShapeResolver = nodeShapeResolver;

    [BindProperty(SupportsGet = true)]
    public int? ProjectId { get; set; }

    public Project? CurrentProject { get; private set; }
    public string CytoscapeElementsJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (!TryLoadProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var nodes = _repository.GetNodes(ProjectId!.Value);
        var relationships = _repository.GetRelationships(ProjectId!.Value);
        var nodeIds = nodes.Select(n => $"n{n.Id}").ToList();
        var savedPositions = _repository.GetLayoutPositions(ProjectId.Value, nodeIds);
        var validParentLookup = BuildValidParentLookup(nodes);

        var elements = new List<object>();

        foreach (var node in nodes)
        {
            var nodeId = $"n{node.Id}";
            var data = new Dictionary<string, object>
            {
                ["id"] = nodeId,
                ["label"] = node.Name,
                ["type"] = node.Type,
                ["shape"] = _nodeShapeResolver.Resolve(node.Type),
                ["isCompoundType"] = _nodeShapeResolver.IsCompoundType(node.Type),
                ["lineColor"] = node.LineColor,
                ["fillColor"] = node.FillColor
            };
            if (validParentLookup.TryGetValue(node.Id, out var parentId))
            {
                data["parent"] = $"n{parentId}";
            }

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
        return Page();
    }

    public async Task<IActionResult> OnPostSaveLayoutAsync()
    {
        if (!TryLoadProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var postedPositions = await JsonSerializer.DeserializeAsync<Dictionary<string, NodeLayoutPosition>>(
            Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var validNodeIds = _repository
            .GetNodes(ProjectId!.Value)
            .Select(n => $"n{n.Id}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filtered = postedPositions
            .Where(p =>
                validNodeIds.Contains(p.Key) &&
                double.IsFinite(p.Value.X) &&
                double.IsFinite(p.Value.Y))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        _repository.SaveLayoutPositions(ProjectId.Value, filtered);

        return new JsonResult(new { success = true, saved = filtered.Count });
    }

    private bool TryLoadProjectAndAuthorize(out IActionResult? redirect)
    {
        redirect = null;

        if (!ProjectId.HasValue)
        {
            redirect = RedirectToPage("/Projects/Index");
            return false;
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            redirect = RedirectToPage("/Account/Login");
            return false;
        }

        var isAdmin = User.IsInRole("Admin");
        if (!_repository.UserCanAccessProject(ProjectId.Value, username, isAdmin))
        {
            redirect = RedirectToPage("/Projects/Index");
            return false;
        }

        CurrentProject = _repository.GetProjectById(ProjectId.Value);
        if (CurrentProject is null)
        {
            redirect = RedirectToPage("/Projects/Index");
            return false;
        }

        return true;
    }

    private Dictionary<int, int> BuildValidParentLookup(IReadOnlyList<Node> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var validParentByNodeId = new Dictionary<int, int>();

        foreach (var node in nodes)
        {
            if (!node.ParentNodeId.HasValue)
            {
                continue;
            }

            var parentId = node.ParentNodeId.Value;
            if (!byId.TryGetValue(parentId, out var parentNode))
            {
                continue;
            }

            if (!_nodeShapeResolver.IsCompoundType(parentNode.Type))
            {
                continue;
            }

            if (CreatesCycle(node.Id, parentId, byId))
            {
                continue;
            }

            validParentByNodeId[node.Id] = parentId;
        }

        return validParentByNodeId;
    }

    private static bool CreatesCycle(int nodeId, int parentId, IReadOnlyDictionary<int, Node> nodesById)
    {
        if (!nodesById.TryGetValue(parentId, out var current))
        {
            return false;
        }

        var visited = new HashSet<int>();
        while (true)
        {
            if (!visited.Add(current.Id))
            {
                return true;
            }

            if (current.Id == nodeId)
            {
                return true;
            }

            if (!current.ParentNodeId.HasValue || !nodesById.TryGetValue(current.ParentNodeId.Value, out current))
            {
                return false;
            }
        }
    }
}
