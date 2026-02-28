using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using dependencies_visualizer.Models;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace dependencies_visualizer.Pages.Nodes;

public sealed class IndexModel(DependencyRepository repository, NodeTypeCatalog nodeTypeCatalog) : PageModel
{
    private readonly DependencyRepository _repository = repository;
    private readonly NodeTypeCatalog _nodeTypeCatalog = nodeTypeCatalog;

    [BindProperty]
    public NodeInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    public IReadOnlyList<Node> Nodes { get; private set; } = [];
    public Project? CurrentProject { get; private set; }
    public IEnumerable<SelectListItem> NodeTypeOptions { get; private set; } = [];
    public IEnumerable<SelectListItem> ParentNodeOptions { get; private set; } = [];
    public bool IsEditing => EditId.HasValue;

    public IActionResult OnGet()
    {
        if (!TryLoadProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        LoadData();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!TryLoadProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var projectNodes = _repository.GetNodes(ProjectId!.Value);

        if (!_nodeTypeCatalog.IsValid(Input.Type))
        {
            ModelState.AddModelError(nameof(Input.Type), "Select a valid node type from configuration.");
        }

        var normalizedName = Input.Name?.Trim() ?? string.Empty;
        var duplicateNameExists = projectNodes.Any(n =>
            string.Equals(n.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
            n.Id != Input.Id);
        if (duplicateNameExists)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Name)}", "A node with this name already exists in the project.");
        }

        if (!Regex.IsMatch(Input.LineColor ?? string.Empty, "^#[0-9a-fA-F]{6}$"))
        {
            ModelState.AddModelError(nameof(Input.LineColor), "Select a valid line color.");
        }

        if (!Regex.IsMatch(Input.FillColor ?? string.Empty, "^#[0-9a-fA-F]{6}$"))
        {
            ModelState.AddModelError(nameof(Input.FillColor), "Select a valid fill color.");
        }

        if (Input.ParentNodeId.HasValue)
        {
            if (Input.ParentNodeId.Value == Input.Id && Input.Id > 0)
            {
                ModelState.AddModelError(nameof(Input.ParentNodeId), "A node cannot be its own parent.");
            }
            else
            {
                var parentNode = _repository.GetNodeById(ProjectId!.Value, Input.ParentNodeId.Value);
                if (parentNode is null)
                {
                    ModelState.AddModelError(nameof(Input.ParentNodeId), "Selected parent node was not found.");
                }
                else if (!_nodeTypeCatalog.IsCompound(parentNode.Type))
                {
                    ModelState.AddModelError(nameof(Input.ParentNodeId), "Selected parent node must be a compound node type.");
                }
                else if (Input.Id > 0 && CreatesParentCycle(Input.Id, Input.ParentNodeId.Value, projectNodes))
                {
                    ModelState.AddModelError(nameof(Input.ParentNodeId), "Parent assignment creates a cycle.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        if (Input.Id > 0)
        {
            var updated = _repository.UpdateNode(ProjectId!.Value, Input.Id, new Node
            {
                Name = normalizedName,
                Type = (Input.Type ?? string.Empty).Trim(),
                ParentNodeId = Input.ParentNodeId,
                LineColor = Input.LineColor ?? "#495057",
                FillColor = Input.FillColor ?? "#ffffff",
                Description = Input.Description
            }, out var updateError);
            if (!updated)
            {
                ModelState.AddModelError(string.Empty, updateError ?? "Unable to update node.");
                EditId = Input.Id;
                LoadData(preservePostedInput: true);
                return Page();
            }

            _repository.AddAuditEntry(
                ProjectId.Value,
                User.Identity!.Name!,
                "Update",
                "Node",
                $"Updated node '{normalizedName}'.");
        }
        else
        {
            _repository.AddNode(ProjectId!.Value, new Node
            {
                Name = normalizedName,
                Type = (Input.Type ?? string.Empty).Trim(),
                ParentNodeId = Input.ParentNodeId,
                LineColor = Input.LineColor ?? "#495057",
                FillColor = Input.FillColor ?? "#ffffff",
                Description = Input.Description
            });

            _repository.AddAuditEntry(
                ProjectId.Value,
                User.Identity!.Name!,
                "Create",
                "Node",
                $"Created node '{normalizedName}'.");
        }

        return RedirectToPage(new { projectId = ProjectId });
    }

    public IActionResult OnPostDelete(int deleteId)
    {
        if (!TryLoadProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var existing = _repository.GetNodeById(ProjectId!.Value, deleteId);
        if (existing is null)
        {
            ModelState.AddModelError(string.Empty, "Node not found.");
            LoadData();
            return Page();
        }

        var deleted = _repository.DeleteNode(ProjectId.Value, deleteId, out var error);
        if (!deleted)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to delete node.");
            LoadData();
            return Page();
        }

        _repository.AddAuditEntry(
            ProjectId.Value,
            User.Identity!.Name!,
            "Delete",
            "Node",
            $"Deleted node '{existing.Name}'.");

        return RedirectToPage(new { projectId = ProjectId });
    }

    public IActionResult OnPostDuplicate(int sourceId)
    {
        if (!TryLoadProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var sourceNode = _repository.GetNodeById(ProjectId!.Value, sourceId);
        if (sourceNode is null)
        {
            ModelState.AddModelError(string.Empty, "Node not found for duplication.");
            LoadData();
            return Page();
        }

        var projectNodes = _repository.GetNodes(ProjectId.Value);
        var duplicateName = BuildDuplicateName(sourceNode.Name, projectNodes);
        _repository.AddNode(ProjectId.Value, new Node
        {
            Name = duplicateName,
            Type = sourceNode.Type,
            ParentNodeId = sourceNode.ParentNodeId,
            LineColor = sourceNode.LineColor,
            FillColor = sourceNode.FillColor,
            Description = sourceNode.Description
        });

        _repository.AddAuditEntry(
            ProjectId.Value,
            User.Identity!.Name!,
            "Duplicate",
            "Node",
            $"Duplicated node '{sourceNode.Name}' as '{duplicateName}'.");

        return RedirectToPage(new { projectId = ProjectId });
    }

    private void LoadData(bool preservePostedInput = false)
    {
        Nodes = _repository.GetNodes(ProjectId!.Value);
        NodeTypeOptions = _nodeTypeCatalog
            .GetNodeTypes()
            .Select(t => new SelectListItem(t, t));

        ParentNodeOptions = Nodes
            .Where(n => _nodeTypeCatalog.IsCompound(n.Type))
            .Where(n => !EditId.HasValue || IsValidParentOption(EditId.Value, n.Id, Nodes))
            .Select(n => new SelectListItem($"{n.Name} ({n.Type})", n.Id.ToString()))
            .ToList();

        if (EditId.HasValue && !preservePostedInput)
        {
            var editNode = _repository.GetNodeById(ProjectId!.Value, EditId.Value);
            if (editNode is null)
            {
                EditId = null;
                return;
            }

            Input = new NodeInputModel
            {
                Id = editNode.Id,
                Name = editNode.Name,
                Type = editNode.Type,
                ParentNodeId = editNode.ParentNodeId,
                LineColor = editNode.LineColor,
                FillColor = editNode.FillColor,
                Description = editNode.Description
            };
        }
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

    private static bool CreatesParentCycle(int nodeId, int parentNodeId, IReadOnlyList<Node> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        if (!byId.TryGetValue(parentNodeId, out var current))
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

            if (!current.ParentNodeId.HasValue || !byId.TryGetValue(current.ParentNodeId.Value, out current))
            {
                return false;
            }
        }
    }

    private static bool IsValidParentOption(int nodeId, int candidateParentId, IReadOnlyList<Node> nodes)
    {
        if (nodeId == candidateParentId)
        {
            return false;
        }

        return !CreatesParentCycle(nodeId, candidateParentId, nodes);
    }

    private static string BuildDuplicateName(string baseName, IReadOnlyList<Node> existingNodes)
    {
        var existingNames = existingNodes
            .Select(n => n.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidate = $"{baseName} (Copy)";
        if (!existingNames.Contains(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (true)
        {
            candidate = $"{baseName} (Copy {suffix})";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }

            suffix++;
        }
    }

    public sealed class NodeInputModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Type { get; set; } = string.Empty;

        [Display(Name = "Parent Compound Node")]
        public int? ParentNodeId { get; set; }

        [Display(Name = "Line Color")]
        [Required]
        [RegularExpression("^#[0-9a-fA-F]{6}$")]
        public string LineColor { get; set; } = "#495057";

        [Display(Name = "Fill Color")]
        [Required]
        [RegularExpression("^#[0-9a-fA-F]{6}$")]
        public string FillColor { get; set; } = "#ffffff";

        [StringLength(400)]
        public string? Description { get; set; }
    }
}
