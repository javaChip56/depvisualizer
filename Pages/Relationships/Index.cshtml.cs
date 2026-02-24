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
    public int? EditId { get; set; }

    public IReadOnlyList<Node> Nodes { get; private set; } = [];
    public IReadOnlyList<DependencyRelationship> Relationships { get; private set; } = [];
    public bool IsEditing => EditId.HasValue;

    public IEnumerable<SelectListItem> NodeOptions => Nodes
        .Select(n => new SelectListItem($"{n.Name} ({n.Type})", n.Id.ToString()));

    public void OnGet()
    {
        LoadData();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        var selectedDependsOnRelated = Input.RelationshipType == RelationshipType.DependsOn;
        string? error;
        var ok = Input.Id > 0
            ? _repository.UpdateRelationship(Input.Id, Input.SelectedNodeId, Input.RelatedNodeId, selectedDependsOnRelated, out error)
            : _repository.AddRelationship(Input.SelectedNodeId, Input.RelatedNodeId, selectedDependsOnRelated, out error);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to save relationship.");
            EditId = Input.Id > 0 ? Input.Id : null;
            LoadData(preservePostedInput: true);
            return Page();
        }

        return RedirectToPage();
    }

    private void LoadData(bool preservePostedInput = false)
    {
        Nodes = _repository.GetNodes();
        Relationships = _repository.GetRelationships();

        if (EditId.HasValue && !preservePostedInput)
        {
            var relationship = _repository.GetRelationshipById(EditId.Value);
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
