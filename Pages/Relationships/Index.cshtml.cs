using dependencies_visualizer.Models;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace dependencies_visualizer.Pages.Relationships;

public sealed class IndexModel(DependencyRepository repository) : PageModel
{
    private readonly DependencyRepository _repository = repository;

    [BindProperty]
    public RelationshipInputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SubProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    public Project? CurrentProject { get; private set; }
    public SubProject? CurrentSubProject { get; private set; }
    public IReadOnlyList<Node> Nodes { get; private set; } = [];
    public IReadOnlyList<DependencyRelationship> Relationships { get; private set; } = [];
    public bool IsEditing => EditId.HasValue;

    public IEnumerable<SelectListItem> NodeOptions => Nodes
        .Select(n => new SelectListItem($"{n.Name} ({n.Type})", n.Id.ToString()));

    public IActionResult OnGet()
    {
        if (!TryLoadSubProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        LoadData();
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!TryLoadSubProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        if (!ModelState.IsValid)
        {
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        var selectedDependsOnRelated = true;
        if (!Enum.IsDefined(Input.ArrowDirection))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.ArrowDirection)}", "Select a valid arrow direction.");
        }

        if (!Enum.IsDefined(Input.LineStyle))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.LineStyle)}", "Select a valid line style.");
        }

        if (!ModelState.IsValid)
        {
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        string? error;
        var ok = Input.Id > 0
            ? _repository.UpdateRelationship(
                SubProjectId!.Value,
                Input.Id,
                Input.SelectedNodeId,
                Input.RelatedNodeId,
                selectedDependsOnRelated,
                Input.Label,
                Input.ArrowDirection,
                Input.LineStyle,
                out error)
            : _repository.AddRelationship(
                SubProjectId!.Value,
                Input.SelectedNodeId,
                Input.RelatedNodeId,
                selectedDependsOnRelated,
                Input.Label,
                Input.ArrowDirection,
                Input.LineStyle,
                out error);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to save relationship.");
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        var selectedNode = Nodes.FirstOrDefault(n => n.Id == Input.SelectedNodeId);
        var relatedNode = Nodes.FirstOrDefault(n => n.Id == Input.RelatedNodeId);
        var selectedName = selectedNode?.Name ?? Input.SelectedNodeId.ToString();
        var relatedName = relatedNode?.Name ?? Input.RelatedNodeId.ToString();
        _repository.AddAuditEntry(
            CurrentProject!.Id,
            User.Identity!.Name!,
            Input.Id > 0 ? "Update" : "Create",
            "Relationship",
            $"{selectedName} {Input.Label} {relatedName} in sub project '{CurrentSubProject!.Name}'.");

        return RedirectToPage(new { subProjectId = SubProjectId });
    }

    public IActionResult OnPostDelete(int deleteId)
    {
        if (!TryLoadSubProjectAndAuthorize(out var redirect))
        {
            return redirect!;
        }

        var existing = _repository.GetRelationshipById(SubProjectId!.Value, deleteId);
        if (existing is null)
        {
            ModelState.AddModelError(string.Empty, "Relationship not found.");
            LoadData();
            return Page();
        }

        var deleted = _repository.DeleteRelationship(SubProjectId.Value, deleteId, out var error);
        if (!deleted)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to delete relationship.");
            LoadData();
            return Page();
        }

        var nodes = _repository.GetNodes(SubProjectId.Value);
        var source = nodes.FirstOrDefault(n => n.Id == existing.SourceNodeId)?.Name ?? existing.SourceNodeId.ToString();
        var target = nodes.FirstOrDefault(n => n.Id == existing.TargetNodeId)?.Name ?? existing.TargetNodeId.ToString();
        _repository.AddAuditEntry(
            CurrentProject!.Id,
            User.Identity!.Name!,
            "Delete",
            "Relationship",
            $"Deleted relationship {source} {existing.Label} {target} from sub project '{CurrentSubProject!.Name}'.");

        return RedirectToPage(new { subProjectId = SubProjectId });
    }

    private void LoadData(bool preservePostedInput = false)
    {
        Nodes = _repository.GetNodes(SubProjectId!.Value);
        Relationships = _repository.GetRelationships(SubProjectId!.Value);

        if (EditId.HasValue && !preservePostedInput)
        {
            var relationship = _repository.GetRelationshipById(SubProjectId!.Value, EditId.Value);
            if (relationship is null)
            {
                EditId = null;
                return;
            }

            Input = new RelationshipInputModel
            {
                Id = relationship.Id,
                SelectedNodeId = relationship.SourceNodeId,
                RelatedNodeId = relationship.TargetNodeId,
                Label = relationship.Label,
                ArrowDirection = relationship.ArrowDirection,
                LineStyle = relationship.LineStyle
            };
        }
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

    public sealed class RelationshipInputModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int SelectedNodeId { get; set; }

        [Range(1, int.MaxValue)]
        public int RelatedNodeId { get; set; }

        [Required]
        [StringLength(50)]
        public string Label { get; set; } = "depends on";

        public RelationshipArrowDirection ArrowDirection { get; set; } = RelationshipArrowDirection.Down;
        public RelationshipLineStyle LineStyle { get; set; } = RelationshipLineStyle.Solid;
    }
}
