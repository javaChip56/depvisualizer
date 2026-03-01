using dependencies_visualizer.Models;

namespace dependencies_visualizer.Services;

public sealed class DependencyRepository
{
    private readonly object _syncRoot = new();
    private readonly List<Project> _projects = [];
    private readonly Dictionary<int, Dictionary<string, ProjectMemberRole>> _projectMembersByProjectId = [];
    private readonly List<SubProject> _subProjects = [];
    private readonly Dictionary<int, Dictionary<string, ProjectMemberRole>> _subProjectMembersBySubProjectId = [];
    private readonly List<Node> _nodes = [];
    private readonly List<DependencyRelationship> _relationships = [];
    private readonly Dictionary<int, List<ProjectAuditEntry>> _auditEntriesByProjectId = [];
    private readonly Dictionary<int, Dictionary<string, NodeLayoutPosition>> _maintainerLayoutBySubProjectId = [];
    private readonly Dictionary<int, Dictionary<string, Dictionary<string, NodeLayoutPosition>>> _contributorLayoutBySubProjectId = [];
    private readonly Dictionary<int, Dictionary<string, EdgeLayoutAdjustment>> _maintainerEdgeLayoutBySubProjectId = [];
    private readonly Dictionary<int, Dictionary<string, Dictionary<string, EdgeLayoutAdjustment>>> _contributorEdgeLayoutBySubProjectId = [];
    private int _nextProjectId = 1;
    private int _nextSubProjectId = 1;
    private int _nextNodeId = 1;
    private int _nextRelationshipId = 1;

    public IReadOnlyList<ProjectAccessSummary> GetProjectsForUser(string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            return _projects
                .Select(project =>
                {
                    var projectRole = GetProjectRoleUnsafe(project.Id, normalizedUsername, isAdmin);
                    if (projectRole != ProjectMemberRole.None)
                    {
                        return new ProjectAccessSummary(project, projectRole);
                    }

                    var subProjectRole = GetHighestSubProjectRoleForProjectUnsafe(project.Id, normalizedUsername, isAdmin);
                    // Project row reflects project-level access only; subproject-only access maps to Contributor.
                    var projectDisplayRole = subProjectRole == ProjectMemberRole.None
                        ? ProjectMemberRole.None
                        : ProjectMemberRole.Contributor;
                    return new ProjectAccessSummary(project, projectDisplayRole);
                })
                .Where(x => x.Role != ProjectMemberRole.None)
                .OrderBy(x => x.Project.Name)
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

    public SubProject? GetSubProjectById(int subProjectId)
    {
        lock (_syncRoot)
        {
            return _subProjects.FirstOrDefault(sp => sp.Id == subProjectId);
        }
    }

    public ProjectMemberRole GetUserRoleForProject(int projectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            return GetProjectRoleUnsafe(projectId, normalizedUsername, isAdmin);
        }
    }

    public ProjectMemberRole GetUserRoleForSubProject(int subProjectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            return GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin);
        }
    }

    public bool UserCanAccessProject(int projectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            if (GetProjectRoleUnsafe(projectId, normalizedUsername, isAdmin) != ProjectMemberRole.None)
            {
                return true;
            }

            return _subProjects
                .Where(sp => sp.ProjectId == projectId)
                .Any(sp => GetSubProjectRoleUnsafe(sp.Id, normalizedUsername, isAdmin) != ProjectMemberRole.None);
        }
    }

    public bool UserCanManageProject(int projectId, string username, bool isAdmin)
    {
        return GetUserRoleForProject(projectId, username, isAdmin) == ProjectMemberRole.Maintainer;
    }

    public bool UserCanAccessSubProject(int subProjectId, string username, bool isAdmin)
    {
        return GetUserRoleForSubProject(subProjectId, username, isAdmin) != ProjectMemberRole.None;
    }

    public bool UserCanManageSubProject(int subProjectId, string username, bool isAdmin)
    {
        return GetUserRoleForSubProject(subProjectId, username, isAdmin) == ProjectMemberRole.Maintainer;
    }

    public IReadOnlyList<ProjectMemberSummary> GetProjectMembers(int projectId)
    {
        lock (_syncRoot)
        {
            if (!_projectMembersByProjectId.TryGetValue(projectId, out var members))
            {
                return [];
            }

            return members.OrderBy(m => m.Key).Select(m => new ProjectMemberSummary(m.Key, m.Value)).ToList();
        }
    }

    public IReadOnlyList<SubProjectAccessSummary> GetSubProjectsForProject(int projectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            var canManageProject = GetProjectRoleUnsafe(projectId, normalizedUsername, isAdmin) == ProjectMemberRole.Maintainer;
            return _subProjects
                .Where(sp => sp.ProjectId == projectId)
                .Select(sp =>
                {
                    var directRole = GetDirectSubProjectRoleUnsafe(sp.Id, normalizedUsername);
                    var role = directRole != ProjectMemberRole.None
                        ? directRole
                        : (canManageProject ? ProjectMemberRole.Maintainer : GetSubProjectRoleUnsafe(sp.Id, normalizedUsername, isAdmin));
                    return new SubProjectAccessSummary(sp, role);
                })
                .Where(x => x.Role != ProjectMemberRole.None)
                .OrderBy(x => x.SubProject.Name)
                .ToList();
        }
    }

    public IReadOnlyList<SubProjectMemberSummary> GetSubProjectMembers(int subProjectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            if (GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin) == ProjectMemberRole.None)
            {
                return [];
            }

            if (!_subProjectMembersBySubProjectId.TryGetValue(subProjectId, out var members))
            {
                return [];
            }

            return members.OrderBy(m => m.Key).Select(m => new SubProjectMemberSummary(m.Key, m.Value)).ToList();
        }
    }

    public IReadOnlyList<ProjectAuditEntry> GetAuditEntries(int projectId, string username, bool isAdmin)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            if (!UserCanAccessProject(projectId, normalizedUsername, isAdmin))
            {
                return [];
            }

            if (!_auditEntriesByProjectId.TryGetValue(projectId, out var entries))
            {
                return [];
            }

            return entries.OrderByDescending(e => e.TimestampUtc).ToList();
        }
    }

    public void AddAuditEntry(int projectId, string username, string action, string entityType, string details)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            if (!_projects.Any(p => p.Id == projectId))
            {
                return;
            }

            if (!_auditEntriesByProjectId.TryGetValue(projectId, out var entries))
            {
                entries = [];
                _auditEntriesByProjectId[projectId] = entries;
            }

            entries.Add(new ProjectAuditEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Username = normalizedUsername,
                Action = action,
                EntityType = entityType,
                Details = details
            });
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
            if (GetProjectRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
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
            if (GetProjectRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
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

            var deletedSubProjectIds = _subProjects.Where(sp => sp.ProjectId == projectId).Select(sp => sp.Id).ToList();
            _subProjects.RemoveAll(sp => sp.ProjectId == projectId);

            foreach (var subProjectId in deletedSubProjectIds)
            {
                _subProjectMembersBySubProjectId.Remove(subProjectId);
                _maintainerLayoutBySubProjectId.Remove(subProjectId);
                _contributorLayoutBySubProjectId.Remove(subProjectId);
                _maintainerEdgeLayoutBySubProjectId.Remove(subProjectId);
                _contributorEdgeLayoutBySubProjectId.Remove(subProjectId);
            }

            _nodes.RemoveAll(n => n.ProjectId == projectId);
            _relationships.RemoveAll(r => r.ProjectId == projectId);
            _auditEntriesByProjectId.Remove(projectId);
            return true;
        }
    }

    public bool AddOrUpdateMember(int projectId, string actorUsername, bool actorIsAdmin, string memberUsername, ProjectMemberRole role, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);
        var normalizedMember = NormalizeUsername(memberUsername);
        if (role == ProjectMemberRole.None)
        {
            error = "Invalid role.";
            return false;
        }

        if (role != ProjectMemberRole.Maintainer)
        {
            error = "Project-level members must be maintainers.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedMember))
        {
            error = "Username is required.";
            return false;
        }

        lock (_syncRoot)
        {
            if (GetProjectRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only maintainers can manage members.";
                return false;
            }

            if (!_projectMembersByProjectId.TryGetValue(projectId, out var members))
            {
                error = "Project not found.";
                return false;
            }

            members[normalizedMember] = role;
            return true;
        }
    }

    public bool RemoveMember(int projectId, string actorUsername, bool actorIsAdmin, string memberUsername, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);
        var normalizedMember = NormalizeUsername(memberUsername);
        if (string.IsNullOrWhiteSpace(normalizedMember))
        {
            error = "Username is required.";
            return false;
        }

        lock (_syncRoot)
        {
            if (GetProjectRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only maintainers can manage members.";
                return false;
            }

            if (!_projectMembersByProjectId.TryGetValue(projectId, out var members))
            {
                error = "Project not found.";
                return false;
            }

            if (!members.TryGetValue(normalizedMember, out var existingRole))
            {
                error = "Member not found in project.";
                return false;
            }

            if (existingRole == ProjectMemberRole.Maintainer)
            {
                var maintainerCount = members.Count(m => m.Value == ProjectMemberRole.Maintainer);
                if (maintainerCount <= 1)
                {
                    error = "A project must have at least one maintainer.";
                    return false;
                }
            }

            members.Remove(normalizedMember);
            return true;
        }
    }

    public bool CreateSubProject(int projectId, string actorUsername, bool actorIsAdmin, string name, string? description, out SubProject? subProject, out string? error)
    {
        subProject = null;
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);

        lock (_syncRoot)
        {
            if (GetProjectRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only project maintainers can create sub projects.";
                return false;
            }

            if (!_projects.Any(p => p.Id == projectId))
            {
                error = "Project not found.";
                return false;
            }

            var trimmedName = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                error = "Sub project name is required.";
                return false;
            }

            var duplicateName = _subProjects.Any(sp => sp.ProjectId == projectId && string.Equals(sp.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
            if (duplicateName)
            {
                error = "A sub project with this name already exists in the project.";
                return false;
            }

            var created = new SubProject
            {
                Id = _nextSubProjectId++,
                ProjectId = projectId,
                Name = trimmedName,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                CreatedUtc = DateTime.UtcNow
            };

            _subProjects.Add(created);
            _subProjectMembersBySubProjectId[created.Id] = new Dictionary<string, ProjectMemberRole>(StringComparer.OrdinalIgnoreCase)
            {
                [normalizedActor] = ProjectMemberRole.Maintainer
            };
            subProject = created;
            return true;
        }
    }

    public bool UpdateSubProject(int projectId, int subProjectId, string actorUsername, bool actorIsAdmin, string name, string? description, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);

        lock (_syncRoot)
        {
            var existing = _subProjects.FirstOrDefault(sp => sp.Id == subProjectId && sp.ProjectId == projectId);
            if (existing is null)
            {
                error = "Sub project not found.";
                return false;
            }

            if (GetSubProjectRoleUnsafe(subProjectId, normalizedActor, actorIsAdmin) == ProjectMemberRole.None)
            {
                error = "Only sub project members can update sub project details.";
                return false;
            }

            var trimmedName = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                error = "Sub project name is required.";
                return false;
            }

            var duplicateName = _subProjects.Any(sp => sp.ProjectId == projectId && sp.Id != subProjectId && string.Equals(sp.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
            if (duplicateName)
            {
                error = "A sub project with this name already exists in the project.";
                return false;
            }

            existing.Name = trimmedName;
            existing.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            return true;
        }
    }

    public bool DeleteSubProject(int projectId, int subProjectId, string actorUsername, bool actorIsAdmin, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);

        lock (_syncRoot)
        {
            if (GetProjectRoleUnsafe(projectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only project maintainers can delete sub projects.";
                return false;
            }

            var existing = _subProjects.FirstOrDefault(sp => sp.Id == subProjectId && sp.ProjectId == projectId);
            if (existing is null)
            {
                error = "Sub project not found.";
                return false;
            }

            _subProjects.Remove(existing);
            _subProjectMembersBySubProjectId.Remove(subProjectId);
            _nodes.RemoveAll(n => n.SubProjectId == subProjectId);
            _relationships.RemoveAll(r => r.SubProjectId == subProjectId);
            _maintainerLayoutBySubProjectId.Remove(subProjectId);
            _contributorLayoutBySubProjectId.Remove(subProjectId);
            _maintainerEdgeLayoutBySubProjectId.Remove(subProjectId);
            _contributorEdgeLayoutBySubProjectId.Remove(subProjectId);
            return true;
        }
    }

    public bool AddOrUpdateSubProjectMember(int projectId, int subProjectId, string actorUsername, bool actorIsAdmin, string memberUsername, ProjectMemberRole role, out string? error)
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
            if (GetSubProjectRoleUnsafe(subProjectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only sub project maintainers can manage sub project members.";
                return false;
            }

            var subProject = _subProjects.FirstOrDefault(sp => sp.Id == subProjectId && sp.ProjectId == projectId);
            if (subProject is null)
            {
                error = "Sub project not found.";
                return false;
            }

            if (!_subProjectMembersBySubProjectId.TryGetValue(subProjectId, out var members))
            {
                members = new Dictionary<string, ProjectMemberRole>(StringComparer.OrdinalIgnoreCase);
                _subProjectMembersBySubProjectId[subProjectId] = members;
            }

            if (members.TryGetValue(normalizedMember, out var existingRole) &&
                existingRole == ProjectMemberRole.Maintainer &&
                role != ProjectMemberRole.Maintainer)
            {
                var maintainerCount = members.Count(m => m.Value == ProjectMemberRole.Maintainer);
                if (maintainerCount <= 1)
                {
                    error = "A sub project must have at least one maintainer.";
                    return false;
                }
            }

            members[normalizedMember] = role;
            return true;
        }
    }

    public bool RemoveSubProjectMember(int projectId, int subProjectId, string actorUsername, bool actorIsAdmin, string memberUsername, out string? error)
    {
        error = null;
        var normalizedActor = NormalizeUsername(actorUsername);
        var normalizedMember = NormalizeUsername(memberUsername);
        if (string.IsNullOrWhiteSpace(normalizedMember))
        {
            error = "Username is required.";
            return false;
        }

        lock (_syncRoot)
        {
            if (GetSubProjectRoleUnsafe(subProjectId, normalizedActor, actorIsAdmin) != ProjectMemberRole.Maintainer)
            {
                error = "Only sub project maintainers can manage sub project members.";
                return false;
            }

            var subProject = _subProjects.FirstOrDefault(sp => sp.Id == subProjectId && sp.ProjectId == projectId);
            if (subProject is null)
            {
                error = "Sub project not found.";
                return false;
            }

            if (!_subProjectMembersBySubProjectId.TryGetValue(subProjectId, out var members))
            {
                error = "Sub project has no members.";
                return false;
            }

            if (!members.TryGetValue(normalizedMember, out var existingRole))
            {
                error = "Member not found in sub project.";
                return false;
            }

            if (existingRole == ProjectMemberRole.Maintainer)
            {
                var maintainerCount = members.Count(m => m.Value == ProjectMemberRole.Maintainer);
                if (maintainerCount <= 1)
                {
                    error = "A sub project must have at least one maintainer.";
                    return false;
                }
            }

            members.Remove(normalizedMember);
            return true;
        }
    }

    public IReadOnlyList<Node> GetNodes(int subProjectId)
    {
        lock (_syncRoot)
        {
            return _nodes.Where(n => n.SubProjectId == subProjectId).OrderBy(n => n.Name).ToList();
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

    public Node? GetNodeById(int subProjectId, int id)
    {
        lock (_syncRoot)
        {
            return _nodes.FirstOrDefault(n => n.SubProjectId == subProjectId && n.Id == id);
        }
    }

    public IReadOnlyList<DependencyRelationship> GetRelationships(int subProjectId)
    {
        lock (_syncRoot)
        {
            return _relationships.Where(r => r.SubProjectId == subProjectId).ToList();
        }
    }

    public DependencyRelationship? GetRelationshipById(int subProjectId, int id)
    {
        lock (_syncRoot)
        {
            return _relationships.FirstOrDefault(r => r.SubProjectId == subProjectId && r.Id == id);
        }
    }

    public Node? AddNode(int subProjectId, Node node)
    {
        lock (_syncRoot)
        {
            var subProject = _subProjects.FirstOrDefault(sp => sp.Id == subProjectId);
            if (subProject is null)
            {
                return null;
            }

            var created = new Node
            {
                Id = _nextNodeId++,
                ProjectId = subProject.ProjectId,
                SubProjectId = subProjectId,
                ParentNodeId = node.ParentNodeId,
                Name = node.Name.Trim(),
                Type = node.Type.Trim(),
                LineColor = NormalizeHexColor(node.LineColor, "#495057"),
                FillColor = NormalizeHexColor(node.FillColor, "#ffffff"),
                Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim()
            };

            _nodes.Add(created);
            return created;
        }
    }

    public bool UpdateNode(int subProjectId, int id, Node node, out string? error)
    {
        lock (_syncRoot)
        {
            error = null;
            var existing = _nodes.FirstOrDefault(n => n.SubProjectId == subProjectId && n.Id == id);
            if (existing is null)
            {
                error = "Node not found.";
                return false;
            }

            existing.Name = node.Name.Trim();
            existing.Type = node.Type.Trim();
            existing.ParentNodeId = node.ParentNodeId;
            existing.LineColor = NormalizeHexColor(node.LineColor, "#495057");
            existing.FillColor = NormalizeHexColor(node.FillColor, "#ffffff");
            existing.Description = string.IsNullOrWhiteSpace(node.Description) ? null : node.Description.Trim();
            return true;
        }
    }

    public bool DeleteNode(int subProjectId, int id, out string? error)
    {
        lock (_syncRoot)
        {
            error = null;
            var existing = _nodes.FirstOrDefault(n => n.SubProjectId == subProjectId && n.Id == id);
            if (existing is null)
            {
                error = "Node not found.";
                return false;
            }

            var hasRelationships = _relationships.Any(r => r.SubProjectId == subProjectId && (r.SourceNodeId == id || r.TargetNodeId == id));
            if (hasRelationships)
            {
                error = "Delete relationships connected to this node before deleting it.";
                return false;
            }

            var hasChildren = _nodes.Any(n => n.SubProjectId == subProjectId && n.ParentNodeId == id);
            if (hasChildren)
            {
                error = "Delete or reassign child nodes before deleting this compound node.";
                return false;
            }

            _nodes.Remove(existing);
            return true;
        }
    }

    public bool AddRelationship(
        int subProjectId,
        int selectedNodeId,
        int relatedNodeId,
        bool selectedDependsOnRelated,
        string label,
        RelationshipArrowDirection arrowDirection,
        RelationshipLineStyle lineStyle,
        out string? error)
    {
        lock (_syncRoot)
        {
            var normalizedLabel = NormalizeRelationshipLabel(label);
            if (normalizedLabel is null)
            {
                error = "Relationship label is required and must be 50 characters or fewer.";
                return false;
            }

            var ok = TryResolveRelationship(subProjectId, selectedNodeId, relatedNodeId, selectedDependsOnRelated, null, out var sourceId, out var targetId, out var projectId, out error);
            if (!ok)
            {
                return false;
            }

            _relationships.Add(new DependencyRelationship
            {
                Id = _nextRelationshipId++,
                ProjectId = projectId,
                SubProjectId = subProjectId,
                SourceNodeId = sourceId,
                TargetNodeId = targetId,
                Label = normalizedLabel,
                ArrowDirection = arrowDirection,
                LineStyle = lineStyle
            });

            return true;
        }
    }

    public bool UpdateRelationship(
        int subProjectId,
        int id,
        int selectedNodeId,
        int relatedNodeId,
        bool selectedDependsOnRelated,
        string label,
        RelationshipArrowDirection arrowDirection,
        RelationshipLineStyle lineStyle,
        out string? error)
    {
        lock (_syncRoot)
        {
            var normalizedLabel = NormalizeRelationshipLabel(label);
            if (normalizedLabel is null)
            {
                error = "Relationship label is required and must be 50 characters or fewer.";
                return false;
            }

            var existingIndex = _relationships.FindIndex(r => r.SubProjectId == subProjectId && r.Id == id);
            if (existingIndex < 0)
            {
                error = "Relationship not found.";
                return false;
            }

            var ok = TryResolveRelationship(subProjectId, selectedNodeId, relatedNodeId, selectedDependsOnRelated, id, out var sourceId, out var targetId, out var projectId, out error);
            if (!ok)
            {
                return false;
            }

            _relationships[existingIndex] = new DependencyRelationship
            {
                Id = id,
                ProjectId = projectId,
                SubProjectId = subProjectId,
                SourceNodeId = sourceId,
                TargetNodeId = targetId,
                Label = normalizedLabel,
                ArrowDirection = arrowDirection,
                LineStyle = lineStyle
            };
            return true;
        }
    }

    private static string? NormalizeRelationshipLabel(string? label)
    {
        var normalized = label?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 50)
        {
            return null;
        }

        return normalized;
    }

    public bool DeleteRelationship(int subProjectId, int id, out string? error)
    {
        lock (_syncRoot)
        {
            error = null;
            var removed = _relationships.RemoveAll(r => r.SubProjectId == subProjectId && r.Id == id) > 0;
            if (!removed)
            {
                error = "Relationship not found.";
                return false;
            }

            return true;
        }
    }

    public IReadOnlyDictionary<string, NodeLayoutPosition> GetLayoutPositions(int subProjectId, string username, bool isAdmin, IEnumerable<string> nodeIds)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            var role = GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin);
            if (role == ProjectMemberRole.None)
            {
                return new Dictionary<string, NodeLayoutPosition>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, NodeLayoutPosition>? positionsByNodeId = null;
            if (role == ProjectMemberRole.Maintainer)
            {
                _maintainerLayoutBySubProjectId.TryGetValue(subProjectId, out positionsByNodeId);
            }
            else
            {
                if (_contributorLayoutBySubProjectId.TryGetValue(subProjectId, out var contributorLayouts) &&
                    contributorLayouts.TryGetValue(normalizedUsername, out var contributorPositions))
                {
                    positionsByNodeId = contributorPositions;
                }
                else
                {
                    _maintainerLayoutBySubProjectId.TryGetValue(subProjectId, out positionsByNodeId);
                }
            }

            if (positionsByNodeId is null)
            {
                return new Dictionary<string, NodeLayoutPosition>(StringComparer.OrdinalIgnoreCase);
            }

            var ids = nodeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return positionsByNodeId.Where(p => ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool SaveLayoutPositions(int subProjectId, string username, bool isAdmin, IDictionary<string, NodeLayoutPosition> positions, out string? error)
    {
        error = null;
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            var role = GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin);
            if (role == ProjectMemberRole.None)
            {
                error = "Access denied.";
                return false;
            }

            var clonedPositions = new Dictionary<string, NodeLayoutPosition>(positions, StringComparer.OrdinalIgnoreCase);
            if (role == ProjectMemberRole.Maintainer)
            {
                _maintainerLayoutBySubProjectId[subProjectId] = clonedPositions;
                return true;
            }

            if (!_contributorLayoutBySubProjectId.TryGetValue(subProjectId, out var contributorLayouts))
            {
                contributorLayouts = new Dictionary<string, Dictionary<string, NodeLayoutPosition>>(StringComparer.OrdinalIgnoreCase);
                _contributorLayoutBySubProjectId[subProjectId] = contributorLayouts;
            }

            contributorLayouts[normalizedUsername] = clonedPositions;
            return true;
        }
    }

    public IReadOnlyDictionary<string, EdgeLayoutAdjustment> GetEdgeLayoutAdjustments(int subProjectId, string username, bool isAdmin, IEnumerable<string> edgeIds)
    {
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            var role = GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin);
            if (role == ProjectMemberRole.None)
            {
                return new Dictionary<string, EdgeLayoutAdjustment>(StringComparer.OrdinalIgnoreCase);
            }

            Dictionary<string, EdgeLayoutAdjustment>? adjustmentsByEdgeId = null;
            if (role == ProjectMemberRole.Maintainer)
            {
                _maintainerEdgeLayoutBySubProjectId.TryGetValue(subProjectId, out adjustmentsByEdgeId);
            }
            else
            {
                if (_contributorEdgeLayoutBySubProjectId.TryGetValue(subProjectId, out var contributorLayouts) &&
                    contributorLayouts.TryGetValue(normalizedUsername, out var contributorAdjustments))
                {
                    adjustmentsByEdgeId = contributorAdjustments;
                }
                else
                {
                    _maintainerEdgeLayoutBySubProjectId.TryGetValue(subProjectId, out adjustmentsByEdgeId);
                }
            }

            if (adjustmentsByEdgeId is null)
            {
                return new Dictionary<string, EdgeLayoutAdjustment>(StringComparer.OrdinalIgnoreCase);
            }

            var ids = edgeIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return adjustmentsByEdgeId
                .Where(p => ids.Contains(p.Key))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool SaveEdgeLayoutAdjustments(int subProjectId, string username, bool isAdmin, IDictionary<string, EdgeLayoutAdjustment> adjustments, out string? error)
    {
        error = null;
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            var role = GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin);
            if (role == ProjectMemberRole.None)
            {
                error = "Access denied.";
                return false;
            }

            var cloned = new Dictionary<string, EdgeLayoutAdjustment>(adjustments, StringComparer.OrdinalIgnoreCase);
            if (role == ProjectMemberRole.Maintainer)
            {
                _maintainerEdgeLayoutBySubProjectId[subProjectId] = cloned;
                return true;
            }

            if (!_contributorEdgeLayoutBySubProjectId.TryGetValue(subProjectId, out var contributorLayouts))
            {
                contributorLayouts = new Dictionary<string, Dictionary<string, EdgeLayoutAdjustment>>(StringComparer.OrdinalIgnoreCase);
                _contributorEdgeLayoutBySubProjectId[subProjectId] = contributorLayouts;
            }

            contributorLayouts[normalizedUsername] = cloned;
            return true;
        }
    }

    public bool ResetContributorLayout(int subProjectId, string username, bool isAdmin, out string? error)
    {
        error = null;
        var normalizedUsername = NormalizeUsername(username);
        lock (_syncRoot)
        {
            var role = GetSubProjectRoleUnsafe(subProjectId, normalizedUsername, isAdmin);
            if (role == ProjectMemberRole.None)
            {
                error = "Access denied.";
                return false;
            }

            if (role == ProjectMemberRole.Maintainer)
            {
                error = "Maintainers use the shared sub project layout.";
                return false;
            }

            if (_contributorLayoutBySubProjectId.TryGetValue(subProjectId, out var contributorLayouts))
            {
                contributorLayouts.Remove(normalizedUsername);
                if (contributorLayouts.Count == 0)
                {
                    _contributorLayoutBySubProjectId.Remove(subProjectId);
                }
            }

            if (_contributorEdgeLayoutBySubProjectId.TryGetValue(subProjectId, out var contributorEdgeLayouts))
            {
                contributorEdgeLayouts.Remove(normalizedUsername);
                if (contributorEdgeLayouts.Count == 0)
                {
                    _contributorEdgeLayoutBySubProjectId.Remove(subProjectId);
                }
            }

            return true;
        }
    }

    private bool TryResolveRelationship(int subProjectId, int selectedNodeId, int relatedNodeId, bool selectedDependsOnRelated, int? existingRelationshipId, out int sourceId, out int targetId, out int projectId, out string? error)
    {
        sourceId = 0;
        targetId = 0;
        projectId = 0;
        error = null;

        if (selectedNodeId == relatedNodeId)
        {
            error = "A node cannot depend on itself.";
            return false;
        }

        var selected = _nodes.FirstOrDefault(n => n.SubProjectId == subProjectId && n.Id == selectedNodeId);
        var related = _nodes.FirstOrDefault(n => n.SubProjectId == subProjectId && n.Id == relatedNodeId);
        if (selected is null || related is null)
        {
            error = "Selected nodes were not found.";
            return false;
        }

        var resolvedSourceId = selectedDependsOnRelated ? selectedNodeId : relatedNodeId;
        var resolvedTargetId = selectedDependsOnRelated ? relatedNodeId : selectedNodeId;

        var alreadyExists = _relationships.Any(r =>
            r.SubProjectId == subProjectId &&
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
        projectId = selected.ProjectId;
        return true;
    }

    private ProjectMemberRole GetProjectRoleUnsafe(int projectId, string normalizedUsername, bool isAdmin)
    {
        if (isAdmin)
        {
            return ProjectMemberRole.Maintainer;
        }

        if (_projectMembersByProjectId.TryGetValue(projectId, out var members) && members.TryGetValue(normalizedUsername, out var role))
        {
            return role;
        }

        return ProjectMemberRole.None;
    }

    private ProjectMemberRole GetSubProjectRoleUnsafe(int subProjectId, string normalizedUsername, bool isAdmin)
    {
        if (isAdmin)
        {
            return ProjectMemberRole.Maintainer;
        }

        var subProject = _subProjects.FirstOrDefault(sp => sp.Id == subProjectId);
        if (subProject is null)
        {
            return ProjectMemberRole.None;
        }

        var directRole = GetDirectSubProjectRoleUnsafe(subProjectId, normalizedUsername);
        if (directRole != ProjectMemberRole.None)
        {
            return directRole;
        }

        var projectRole = GetProjectRoleUnsafe(subProject.ProjectId, normalizedUsername, isAdmin);
        if (projectRole == ProjectMemberRole.Maintainer)
        {
            return ProjectMemberRole.Maintainer;
        }

        return ProjectMemberRole.None;
    }

    private ProjectMemberRole GetDirectSubProjectRoleUnsafe(int subProjectId, string normalizedUsername)
    {
        if (_subProjectMembersBySubProjectId.TryGetValue(subProjectId, out var members) &&
            members.TryGetValue(normalizedUsername, out var role))
        {
            return role;
        }

        return ProjectMemberRole.None;
    }

    private ProjectMemberRole GetHighestSubProjectRoleForProjectUnsafe(int projectId, string normalizedUsername, bool isAdmin)
    {
        if (isAdmin)
        {
            return ProjectMemberRole.Maintainer;
        }

        var roles = _subProjects.Where(sp => sp.ProjectId == projectId).Select(sp => GetSubProjectRoleUnsafe(sp.Id, normalizedUsername, isAdmin));

        if (roles.Contains(ProjectMemberRole.Maintainer))
        {
            return ProjectMemberRole.Maintainer;
        }

        if (roles.Contains(ProjectMemberRole.Contributor))
        {
            return ProjectMemberRole.Contributor;
        }

        return ProjectMemberRole.None;
    }

    private static string NormalizeUsername(string? username)
    {
        return username?.Trim() ?? string.Empty;
    }

    private static string NormalizeHexColor(string? color, string fallback)
    {
        var value = color?.Trim() ?? string.Empty;
        return System.Text.RegularExpressions.Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$") ? value.ToLowerInvariant() : fallback;
    }
}

public sealed record ProjectAccessSummary(Project Project, ProjectMemberRole Role);

public sealed record ProjectMemberSummary(string Username, ProjectMemberRole Role);

public sealed record SubProjectAccessSummary(SubProject SubProject, ProjectMemberRole Role);

public sealed record SubProjectMemberSummary(string Username, ProjectMemberRole Role);
