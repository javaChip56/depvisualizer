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
    public int? SubProjectId { get; set; }

    public Project? CurrentProject { get; private set; }
    public SubProject? CurrentSubProject { get; private set; }
    public bool IsMaintainer { get; private set; }
    public string CytoscapeElementsJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (!TryLoadSubProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var nodes = _repository.GetNodes(SubProjectId!.Value);
        var relationships = _repository.GetRelationships(SubProjectId!.Value);
        var nodeIds = nodes.Select(n => $"n{n.Id}").ToList();
        var edgeIds = relationships.Select(r => $"e{r.Id}").ToList();
        var savedPositions = _repository.GetLayoutPositions(
            SubProjectId.Value,
            User.Identity!.Name!,
            User.IsInRole("Admin"),
            nodeIds);
        var savedEdgeAdjustments = _repository.GetEdgeLayoutAdjustments(
            SubProjectId.Value,
            User.Identity!.Name!,
            User.IsInRole("Admin"),
            edgeIds);
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
                elements.Add(new { data, position = new { x = savedPosition.X, y = savedPosition.Y } });
            }
            else
            {
                elements.Add(new { data });
            }
        }

        foreach (var relationship in relationships)
        {
            var (sourceArrowShape, targetArrowShape) = relationship.ArrowDirection switch
            {
                RelationshipArrowDirection.Up => ("triangle", "none"),
                RelationshipArrowDirection.Both => ("triangle", "triangle"),
                _ => ("none", "triangle")
            };

            var lineStyle = relationship.LineStyle switch
            {
                RelationshipLineStyle.Dotted => "dotted",
                RelationshipLineStyle.Dashed => "dashed",
                _ => "solid"
            };

            var edgeId = $"e{relationship.Id}";
            var edgeLayout = savedEdgeAdjustments.TryGetValue(edgeId, out var savedEdgeLayout)
                ? savedEdgeLayout
                : new EdgeLayoutAdjustment();

            elements.Add(new
            {
                data = new Dictionary<string, object>
                {
                    ["id"] = edgeId,
                    ["source"] = $"n{relationship.SourceNodeId}",
                    ["target"] = $"n{relationship.TargetNodeId}",
                    ["label"] = relationship.Label,
                    ["sourceArrowShape"] = sourceArrowShape,
                    ["targetArrowShape"] = targetArrowShape,
                    ["lineStyle"] = lineStyle,
                    ["labelOffsetX"] = edgeLayout.LabelOffsetX,
                    ["labelOffsetY"] = edgeLayout.LabelOffsetY
                }
            });
        }

        CytoscapeElementsJson = JsonSerializer.Serialize(elements);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveLayoutAsync()
    {
        if (!TryLoadSubProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var payload = await JsonSerializer.DeserializeAsync<LayoutSavePayload>(
            Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload is null)
        {
            return new JsonResult(new { success = false, error = "Invalid layout payload." });
        }

        var validNodeIds = _repository
            .GetNodes(SubProjectId!.Value)
            .Select(n => $"n{n.Id}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var validEdgeIds = _repository
            .GetRelationships(SubProjectId.Value)
            .Select(r => $"e{r.Id}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var filteredNodePositions = (payload.Nodes ?? new Dictionary<string, NodeLayoutPosition>(StringComparer.OrdinalIgnoreCase))
            .Where(p =>
                validNodeIds.Contains(p.Key) &&
                double.IsFinite(p.Value.X) &&
                double.IsFinite(p.Value.Y))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var filteredEdgeAdjustments = (payload.Edges ?? new Dictionary<string, EdgeLayoutAdjustment>(StringComparer.OrdinalIgnoreCase))
            .Where(p =>
                validEdgeIds.Contains(p.Key) &&
                double.IsFinite(p.Value.LabelOffsetX) &&
                double.IsFinite(p.Value.LabelOffsetY))
            .ToDictionary(
                p => p.Key,
                p => new EdgeLayoutAdjustment
                {
                    LabelOffsetX = p.Value.LabelOffsetX,
                    LabelOffsetY = p.Value.LabelOffsetY
                },
                StringComparer.OrdinalIgnoreCase);

        var savedNodes = _repository.SaveLayoutPositions(
            SubProjectId.Value,
            User.Identity!.Name!,
            User.IsInRole("Admin"),
            filteredNodePositions,
            out var saveError);
        if (!savedNodes)
        {
            return new JsonResult(new { success = false, error = saveError ?? "Unable to save layout." });
        }

        var savedEdges = _repository.SaveEdgeLayoutAdjustments(
            SubProjectId.Value,
            User.Identity!.Name!,
            User.IsInRole("Admin"),
            filteredEdgeAdjustments,
            out var edgeSaveError);
        if (!savedEdges)
        {
            return new JsonResult(new { success = false, error = edgeSaveError ?? "Unable to save edge layout." });
        }

        return new JsonResult(new
        {
            success = true,
            savedNodes = filteredNodePositions.Count,
            savedEdges = filteredEdgeAdjustments.Count
        });
    }

    public IActionResult OnPostResetLayout()
    {
        if (!TryLoadSubProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var reset = _repository.ResetContributorLayout(
            SubProjectId!.Value,
            User.Identity!.Name!,
            User.IsInRole("Admin"),
            out var error);
        if (!reset)
        {
            return new JsonResult(new { success = false, error = error ?? "Unable to reset layout." });
        }

        return new JsonResult(new { success = true });
    }

    private bool TryLoadSubProjectAndAuthorize(out IActionResult? redirect)
    {
        redirect = null;

        if (!SubProjectId.HasValue)
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
        if (!_repository.UserCanAccessSubProject(SubProjectId.Value, username, isAdmin))
        {
            redirect = RedirectToPage("/Projects/Index");
            return false;
        }

        IsMaintainer = _repository.UserCanManageSubProject(SubProjectId.Value, username, isAdmin);

        CurrentSubProject = _repository.GetSubProjectById(SubProjectId.Value);
        if (CurrentSubProject is null)
        {
            redirect = RedirectToPage("/Projects/Index");
            return false;
        }

        CurrentProject = _repository.GetProjectById(CurrentSubProject.ProjectId);
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

    public sealed class LayoutSavePayload
    {
        public Dictionary<string, NodeLayoutPosition>? Nodes { get; set; }
        public Dictionary<string, EdgeLayoutAdjustment>? Edges { get; set; }
    }
}
