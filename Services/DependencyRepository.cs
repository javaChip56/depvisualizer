using dependencies_visualizer.Models;

namespace dependencies_visualizer.Services;

public sealed class DependencyRepository
{
    private readonly object _syncRoot = new();
    private readonly List<Node> _nodes = [];
    private readonly List<DependencyRelationship> _relationships = [];
    private Dictionary<string, NodeLayoutPosition> _layoutPositions = new(StringComparer.OrdinalIgnoreCase);
    private int _nextNodeId = 1;
    private int _nextRelationshipId = 1;

    public IReadOnlyList<Node> GetNodes()
    {
        lock (_syncRoot)
        {
            return _nodes
                .OrderBy(n => n.Name)
                .ToList();
        }
    }

    public Node? GetNodeById(int id)
    {
        lock (_syncRoot)
        {
            return _nodes.FirstOrDefault(n => n.Id == id);
        }
    }

    public IReadOnlyList<DependencyRelationship> GetRelationships()
    {
        lock (_syncRoot)
        {
            return _relationships.ToList();
        }
    }

    public DependencyRelationship? GetRelationshipById(int id)
    {
        lock (_syncRoot)
        {
            return _relationships.FirstOrDefault(r => r.Id == id);
        }
    }

    public Node AddNode(Node node)
    {
        lock (_syncRoot)
        {
            var created = new Node
            {
                Id = _nextNodeId++,
                Name = node.Name.Trim(),
                Type = node.Type,
                Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim()
            };

            _nodes.Add(created);
            return created;
        }
    }

    public bool UpdateNode(int id, Node node, out string? error)
    {
        lock (_syncRoot)
        {
            error = null;
            var existing = _nodes.FirstOrDefault(n => n.Id == id);
            if (existing is null)
            {
                error = "Node not found.";
                return false;
            }

            existing.Name = node.Name.Trim();
            existing.Type = node.Type.Trim();
            existing.Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim();
            return true;
        }
    }

    public bool AddRelationship(int selectedNodeId, int relatedNodeId, bool selectedDependsOnRelated, out string? error)
    {
        lock (_syncRoot)
        {
            var ok = TryResolveRelationship(
                selectedNodeId,
                relatedNodeId,
                selectedDependsOnRelated,
                null,
                out var sourceId,
                out var targetId,
                out error);
            if (!ok)
            {
                return false;
            }

            _relationships.Add(new DependencyRelationship
            {
                Id = _nextRelationshipId++,
                SourceNodeId = sourceId,
                TargetNodeId = targetId
            });

            return true;
        }
    }

    public bool UpdateRelationship(int id, int selectedNodeId, int relatedNodeId, bool selectedDependsOnRelated, out string? error)
    {
        lock (_syncRoot)
        {
            var existingIndex = _relationships.FindIndex(r => r.Id == id);
            if (existingIndex < 0)
            {
                error = "Relationship not found.";
                return false;
            }

            var ok = TryResolveRelationship(
                selectedNodeId,
                relatedNodeId,
                selectedDependsOnRelated,
                id,
                out var sourceId,
                out var targetId,
                out error);
            if (!ok)
            {
                return false;
            }

            _relationships[existingIndex] = new DependencyRelationship
            {
                Id = id,
                SourceNodeId = sourceId,
                TargetNodeId = targetId
            };
            return true;
        }
    }

    public IReadOnlyDictionary<string, NodeLayoutPosition> GetLayoutPositions(IEnumerable<string> nodeIds)
    {
        lock (_syncRoot)
        {
            var ids = nodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _layoutPositions
                .Where(p => ids.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SaveLayoutPositions(IDictionary<string, NodeLayoutPosition> positions)
    {
        lock (_syncRoot)
        {
            _layoutPositions = new Dictionary<string, NodeLayoutPosition>(positions, StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool TryResolveRelationship(
        int selectedNodeId,
        int relatedNodeId,
        bool selectedDependsOnRelated,
        int? existingRelationshipId,
        out int sourceId,
        out int targetId,
        out string? error)
    {
        sourceId = 0;
        targetId = 0;
        error = null;

        if (selectedNodeId == relatedNodeId)
        {
            error = "A node cannot depend on itself.";
            return false;
        }

        var selectedExists = _nodes.Any(n => n.Id == selectedNodeId);
        var relatedExists = _nodes.Any(n => n.Id == relatedNodeId);
        if (!selectedExists || !relatedExists)
        {
            error = "Selected nodes were not found.";
            return false;
        }

        var resolvedSourceId = selectedDependsOnRelated ? selectedNodeId : relatedNodeId;
        var resolvedTargetId = selectedDependsOnRelated ? relatedNodeId : selectedNodeId;

        var alreadyExists = _relationships.Any(r =>
            r.SourceNodeId == resolvedSourceId &&
            r.TargetNodeId == resolvedTargetId &&
            (!existingRelationshipId.HasValue || r.Id != existingRelationshipId.Value));
        if (alreadyExists)
        {
            error = "This relationship already exists.";
            return false;
        }

        sourceId = resolvedSourceId;
        targetId = resolvedTargetId;
        return true;
    }
}
