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
    public int? EditId { get; set; }

    public IReadOnlyList<Node> Nodes { get; private set; } = [];
    public IEnumerable<SelectListItem> NodeTypeOptions { get; private set; } = [];
    public bool IsEditing => EditId.HasValue;

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPost()
    {
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
            var updated = _repository.UpdateNode(Input.Id, new Node
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
            _repository.AddNode(new Node
            {
                Name = Input.Name,
                Type = Input.Type.Trim(),
                Description = Input.Description
            });
        }

        return RedirectToPage();
    }

    private void LoadData(bool preservePostedInput = false)
    {
        Nodes = _repository.GetNodes();
        NodeTypeOptions = _nodeTypeCatalog
            .GetNodeTypes()
            .Select(t => new SelectListItem(t, t));

        if (EditId.HasValue && !preservePostedInput)
        {
            var editNode = _repository.GetNodeById(EditId.Value);
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
