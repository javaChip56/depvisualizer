using dependencies_visualizer.Models;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dependencies_visualizer.Pages.Projects;

public sealed class AuditModel(DependencyRepository repository) : PageModel
{
    private readonly DependencyRepository _repository = repository;

    [BindProperty(SupportsGet = true)]
    public int? ProjectId { get; set; }

    public Project? CurrentProject { get; private set; }
    public IReadOnlyList<ProjectAuditEntry> Entries { get; private set; } = [];

    public IActionResult OnGet()
    {
        if (!ProjectId.HasValue)
        {
            return RedirectToPage("/Projects/Index");
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToPage("/Account/Login");
        }

        var isAdmin = User.IsInRole("Admin");
        if (!_repository.UserCanAccessProject(ProjectId.Value, username, isAdmin))
        {
            return RedirectToPage("/Projects/Index");
        }

        CurrentProject = _repository.GetProjectById(ProjectId.Value);
        if (CurrentProject is null)
        {
            return RedirectToPage("/Projects/Index");
        }

        Entries = _repository.GetAuditEntries(ProjectId.Value, username, isAdmin);
        return Page();
    }
}
