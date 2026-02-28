using dependencies_visualizer.Models;

namespace dependencies_visualizer.Services;

public sealed class DependencyRepository
{
    private readonly object _syncRoot = new();
    private readonly List<Project> _projects = [];
    private readonly Dictionary<int, Dictionary<string, ProjectMemberRole>> _projectMembersByProjectId = [];
    private readonly List<Node> _nodes = [];
    private readonly List<DependencyRelationship> _relationships = [];
    private readonly Dictionary<int, Dictionary<string, NodeLayoutPosition>> _layoutPositionsByProjectId = [];
    private int _nextProjectId = 1;
    private int _nextNodeId = 1;
    private int _nextRelationshipId = 1;

    public IReadOnlyList<ProjectAccessSummary> GetProjectsForUser(string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            return _projects
                .Select(p => new
                {
                    Project = p,
                    Role = GetRoleUnsafe(p.Id, normalizedUsername, isAdmin)
                })
                .Where(x => x.Role != ProjectMemberRole.None)
                .OrderBy(x => x.Project.Name)
                .Select(x => new ProjectAccessSummary(x.Project, x.Role))
                .ToList();
        }
    }

    public Project? GetProjectById(int projectId)
    {
        lock (_syncRoot)
        {
            return _projects.FirstOrDefault(p => p.Id == projectId);
        }
    }

    public ProjectMemberRole GetUserRoleForProject(int projectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            return GetRoleUnsafe(projectId, normalizedUsername, isAdmin);
        }
    }

    public bool UserCanAccessProject(int projectId, string username, bool isAdmin)
    {
        return GetUserRoleForProject(projectId, username, isAdmin) != ProjectMemberRole.None;
    }

    public bool UserCanManageProject(int projectId, string username, bool isAdmin)
    {
        return GetUserRoleForProject(projectId, username, isAdmin) == ProjectMemberRole.Maintainer;
    }

    public IReadOnlyList<ProjectMemberSummary> GetProjectMembers(int projectId)
    {
        lock (_syncRoot)
        {
            if (!_projectMembersByProjectId.TryGetValue(projectId, out var members))
            {
                return [];
            }

            return members
                .OrderBy(m => m.Key)
                .Select(m => new ProjectMemberSummary(m.Key, m.Value))
                .ToList();
        }
    }

    public Project CreateProject(string name, string? description, string ownerUsername)
    {
        var normalizedOwner = NormalizeUsername(ownerUsername);
        lock (_syncRoot)
        {
            var created = new Project
            {
                Id = _nextProjectId++,
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                OwnerUsername = normalizedOwner,
                CreatedUtc = DateTime.UtcNow
            };

            _projects.Add(created);
            _projectMembersByProjectId[created.Id] = new Dictionary<string, ProjectMemberRole>(StringComparer.OrdinalIgnoreCase)
            {
                [normalizedOwner] = ProjectMemberRole.Maintainer
            };

            return created;
        }
    }

    public bool UpdateProject(int projectId, string actorUsername, bool actorIsAdmin, string name, string? description, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);
        lock (_syncRoot)
        {
            if (GetRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only maintainers can update project details.";
                return false;
            }

            var existing = _projects.FirstOrDefault(p => p.Id == projectId);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            existing.Name = name.Trim();
            existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            return true;
        }
    }

    public bool DeleteProject(int projectId, string actorUsername, bool actorIsAdmin, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);
        lock (_syncRoot)
        {
            if (GetRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only maintainers can delete projects.";
                return false;
            }

            var removed = _projects.RemoveAll(p => p.Id == projectId) > 0;
            if (!removed)
            {
                error = "Project not found.";
                return false;
            }

            _projectMembersByProjectId.Remove(projectId);
            _nodes.RemoveAll(n => n.ProjectId == projectId);
            _relationships.RemoveAll(r => r.ProjectId == projectId);
            _layoutPositionsByProjectId.Remove(projectId);
            return true;
        }
    }

    public bool AddOrUpdateMember(
        int projectId,
        string actorUsername,
        bool actorIsAdmin,
        string memberUsername,
        ProjectMemberRole role,
        out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);
        var normalizedMember = NormalizeUsername(memberUsername);
        if (role == ProjectMemberRole.None)
        {
            error = "Invalid role.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedMember))
        {
            error = "Username is required.";
            return false;
        }

        lock (_syncRoot)
        {
            if (GetRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only maintainers can manage members.";
                return false;
            }

            if (!_projectMembersByProjectId.TryGetValue(projectId, out var members))
            {
                error = "Project not found.";
                return false;
            }

            if (members.TryGetValue(normalizedMember, out var existingRole) &&
                existingRole == ProjectMemberRole.Maintainer &&
                role != ProjectMemberRole.Maintainer)
            {
                var maintainerCount = members.Count(m => m.Value == ProjectMemberRole.Maintainer);
                if (maintainerCount <= 1)
                {
                    error = "A project must have at least one maintainer.";
                    return false;
                }
            }

            members[normalizedMember] = role;
            return true;
        }
    }

    public IReadOnlyList<Node> GetNodes(int projectId)
    {
        lock (_syncRoot)
        {
            return _nodes
                .Where(n => n.ProjectId == projectId)
                .OrderBy(n => n.Name)
                .ToList();
        }
    }

    public bool IsNodeTypeInUse(string nodeType)
    {
        var normalized = nodeType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        lock (_syncRoot)
        {
            return _nodes.Any(n => string.Equals(n.Type, normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public Node? GetNodeById(int projectId, int id)
    {
        lock (_syncRoot)
        {
            return _nodes.FirstOrDefault(n => n.ProjectId == projectId && n.Id == id);
        }
    }

    public IReadOnlyList<DependencyRelationship> GetRelationships(int projectId)
    {
        lock (_syncRoot)
        {
            return _relationships
                .Where(r => r.ProjectId == projectId)
                .ToList();
        }
    }

    public DependencyRelationship? GetRelationshipById(int projectId, int id)
    {
        lock (_syncRoot)
        {
            return _relationships.FirstOrDefault(r => r.ProjectId == projectId && r.Id == id);
        }
    }

    public Node AddNode(int projectId, Node node)
    {
        lock (_syncRoot)
        {
            var created = new Node
            {
                Id = _nextNodeId++,
                ProjectId = projectId,
                ParentNodeId = node.ParentNodeId,
                Name = node.Name.Trim(),
                Type = node.Type.Trim(),
                Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim()
            };

            _nodes.Add(created);
            return created;
        }
    }

    public bool UpdateNode(int projectId, int id, Node node, out string? error)
    {
        lock (_syncRoot)
        {
            error = null;
            var existing = _nodes.FirstOrDefault(n => n.ProjectId == projectId && n.Id == id);
            if (existing is null)
            {
                error = "Node not found.";
                return false;
            }

            existing.Name = node.Name.Trim();
            existing.Type = node.Type.Trim();
            existing.ParentNodeId = node.ParentNodeId;
            existing.Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim();
            return true;
        }
    }

    public bool AddRelationship(int projectId, int selectedNodeId, int relatedNodeId, bool selectedDependsOnRelated, out string? error)
    {
        lock (_syncRoot)
        {
            var ok = TryResolveRelationship(
                projectId,
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
                ProjectId = projectId,
                SourceNodeId = sourceId,
                TargetNodeId = targetId
            });

            return true;
        }
    }

    public bool UpdateRelationship(int projectId, int id, int selectedNodeId, int relatedNodeId, bool selectedDependsOnRelated, out string? error)
    {
        lock (_syncRoot)
        {
            var existingIndex = _relationships.FindIndex(r => r.ProjectId == projectId && r.Id == id);
            if (existingIndex < 0)
            {
                error = "Relationship not found.";
                return false;
            }

            var ok = TryResolveRelationship(
                projectId,
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
                ProjectId = projectId,
                SourceNodeId = sourceId,
                TargetNodeId = targetId
            };
            return true;
        }
    }

    public IReadOnlyDictionary<string, NodeLayoutPosition> GetLayoutPositions(int projectId, IEnumerable<string> nodeIds)
    {
        lock (_syncRoot)
        {
            if (!_layoutPositionsByProjectId.TryGetValue(projectId, out var positionsByNodeId))
            {
                return new Dictionary<string, NodeLayoutPosition>(StringComparer.OrdinalIgnoreCase);
            }

            var ids = nodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return positionsByNodeId
                .Where(p => ids.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void SaveLayoutPositions(int projectId, IDictionary<string, NodeLayoutPosition> positions)
    {
        lock (_syncRoot)
        {
            _layoutPositionsByProjectId[projectId] = new Dictionary<string, NodeLayoutPosition>(positions, StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool TryResolveRelationship(
        int projectId,
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

        var selectedExists = _nodes.Any(n => n.ProjectId == projectId && n.Id == selectedNodeId);
        var relatedExists = _nodes.Any(n => n.ProjectId == projectId && n.Id == relatedNodeId);
        if (!selectedExists || !relatedExists)
        {
            error = "Selected nodes were not found.";
            return false;
        }

        var resolvedSourceId = selectedDependsOnRelated ? selectedNodeId : relatedNodeId;
        var resolvedTargetId = selectedDependsOnRelated ? relatedNodeId : selectedNodeId;

        var alreadyExists = _relationships.Any(r =>
            r.ProjectId == projectId &&
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

    private ProjectMemberRole GetRoleUnsafe(int projectId, string normalizedUsername, bool isAdmin)
    {
        if (isAdmin)
        {
            return ProjectMemberRole.Maintainer;
        }

        if (_projectMembersByProjectId.TryGetValue(projectId, out var members) &&
            members.TryGetValue(normalizedUsername, out var role))
        {
            return role;
        }

        return ProjectMemberRole.None;
    }

    private static string NormalizeUsername(string? username)
    {
        return username?.Trim() ?? string.Empty;
    }
}

public sealed record ProjectAccessSummary(Project Project, ProjectMemberRole Role);

public sealed record ProjectMemberSummary(string Username, ProjectMemberRole Role);
