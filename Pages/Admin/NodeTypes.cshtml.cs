using System.ComponentModel.DataAnnotations;
using dependencies_visualizer.Models;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace dependencies_visualizer.Pages.Admin;

[Authorize(Roles = "Admin")]
public sealed class NodeTypesModel(NodeTypeAdminService nodeTypeAdminService) : PageModel
{
    private readonly NodeTypeAdminService _nodeTypeAdminService = nodeTypeAdminService;

    [BindProperty]
    [ValidateNever]
    public NodeTypeInputModel Input { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public DeleteNodeTypeInputModel DeleteInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? EditName { get; set; }

    public bool IsEditMode => !string.IsNullOrWhiteSpace(EditName);
    public IReadOnlyList<NodeTypeDefinition> NodeTypes { get; private set; } = [];

    public IEnumerable<SelectListItem> ShapeOptions => CytoscapeShapeCatalog.ValidShapes
        .Select(s => new SelectListItem(s, s));

    public void OnGet()
    {
        LoadNodeTypes();
        if (!string.IsNullOrWhiteSpace(EditName))
        {
            var selected = NodeTypes.FirstOrDefault(n =>
                string.Equals(n.Name, EditName, StringComparison.OrdinalIgnoreCase));
            if (selected is not null)
            {
                Input = new NodeTypeInputModel
                {
                    Name = selected.Name,
                    Shape = selected.Shape,
                    Kind = selected.Kind
                };
            }
        }
    }

    public IActionResult OnPostSave()
    {
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            LoadNodeTypes();
            return Page();
        }

        var saved = _nodeTypeAdminService.Upsert(Input.Name, Input.Shape, Input.Kind, out var error);
        if (!saved)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to save node type.");
            LoadNodeTypes();
            return Page();
        }

        return RedirectToPage();
    }

    public IActionResult OnPostDelete()
    {
        ModelState.Clear();
        if (!TryValidateModel(DeleteInput, nameof(DeleteInput)))
        {
            LoadNodeTypes();
            return Page();
        }

        var deleted = _nodeTypeAdminService.Delete(DeleteInput.Name, out var error);
        if (!deleted)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to delete node type.");
            LoadNodeTypes();
            return Page();
        }

        return RedirectToPage();
    }

    private void LoadNodeTypes()
    {
        NodeTypes = _nodeTypeAdminService.GetDefinitions();
    }

    public sealed class NodeTypeInputModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Shape { get; set; } = "ellipse";

        [Required]
        public NodeTypeKind Kind { get; set; } = NodeTypeKind.Regular;
    }

    public sealed class DeleteNodeTypeInputModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
