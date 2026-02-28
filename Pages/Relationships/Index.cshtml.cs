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
    public int? ProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? EditId { get; set; }

    public Project? CurrentProject { get; private set; }
    public IReadOnlyList<Node> Nodes { get; private set; } = [];
    public IReadOnlyList<DependencyRelationship> Relationships { get; private set; } = [];
    public bool IsEditing => EditId.HasValue;

    public IEnumerable<SelectListItem> NodeOptions => Nodes
        .Select(n => new SelectListItem($"{n.Name} ({n.Type})", n.Id.ToString()));

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

        if (!ModelState.IsValid)
        {
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        var selectedDependsOnRelated = Input.RelationshipType == RelationshipType.DependsOn;
        string? error;
        var ok = Input.Id > 0
            ? _repository.UpdateRelationship(ProjectId!.Value, Input.Id, Input.SelectedNodeId, Input.RelatedNodeId, selectedDependsOnRelated, out error)
            : _repository.AddRelationship(ProjectId!.Value, Input.SelectedNodeId, Input.RelatedNodeId, selectedDependsOnRelated, out error);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to save relationship.");
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        return RedirectToPage(new { projectId = ProjectId });
    }

    private void LoadData(bool preservePostedInput = false)
    {
        Nodes = _repository.GetNodes(ProjectId!.Value);
        Relationships = _repository.GetRelationships(ProjectId!.Value);

        if (EditId.HasValue && !preservePostedInput)
        {
            var relationship = _repository.GetRelationshipById(ProjectId!.Value, EditId.Value);
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
                RelationshipType = RelationshipType.DependsOn
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

    public sealed class RelationshipInputModel
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int SelectedNodeId { get; set; }

        [Range(1, int.MaxValue)]
        public int RelatedNodeId { get; set; }
        public RelationshipType RelationshipType { get; set; } = RelationshipType.DependsOn;
    }

    public enum RelationshipType
    {
        DependsOn,
        DependencyOf
    }
}
