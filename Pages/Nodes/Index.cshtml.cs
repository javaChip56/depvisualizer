using System.ComponentModel.DataAnnotations;
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

        if (!_nodeTypeCatalog.IsValid(Input.Type))
        {
            ModelState.AddModelError(nameof(Input.Type), "Select a valid node type from configuration.");
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
                Name = Input.Name,
                Type = Input.Type.Trim(),
                Description = Input.Description
            }, out var updateError);
            if (!updated)
            {
                ModelState.AddModelError(string.Empty, updateError ?? "Unable to update node.");
                EditId = Input.Id;
                LoadData(preservePostedInput: true);
                return Page();
            }
        }
        else
        {
            _repository.AddNode(ProjectId!.Value, new Node
            {
                Name = Input.Name,
                Type = Input.Type.Trim(),
                Description = Input.Description
            });
        }

        return RedirectToPage(new { projectId = ProjectId });
    }

    private void LoadData(bool preservePostedInput = false)
    {
        Nodes = _repository.GetNodes(ProjectId!.Value);
        NodeTypeOptions = _nodeTypeCatalog
            .GetNodeTypes()
            .Select(t => new SelectListItem(t, t));

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

    public sealed class NodeInputModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Type { get; set; } = string.Empty;

        [StringLength(400)]
        public string? Description { get; set; }
    }
}
