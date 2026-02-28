using System.ComponentModel.DataAnnotations;
using dependencies_visualizer.Models;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace dependencies_visualizer.Pages.Projects;

public sealed class IndexModel(DependencyRepository repository, UserAccountService userAccountService) : PageModel
{
    private readonly DependencyRepository _repository = repository;
    private readonly UserAccountService _userAccountService = userAccountService;

    [BindProperty]
    [ValidateNever]
    public CreateProjectInputModel CreateInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public UpdateProjectInputModel UpdateInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public AddMemberInputModel MemberInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ManageProjectId { get; set; }

    public IReadOnlyList<ProjectAccessSummary> Projects { get; private set; } = [];
    public Project? ManagedProject { get; private set; }
    public bool CanManageSelectedProject { get; private set; }
    public IReadOnlyList<ProjectMemberSummary> Members { get; private set; } = [];
    public IEnumerable<SelectListItem> MemberUserOptions { get; private set; } = [];

    public void OnGet()
    {
        LoadPageData();
    }

    public IActionResult OnPostCreateProject()
    {
        ModelState.Clear();
        if (!TryValidateModel(CreateInput, nameof(CreateInput)))
        {
            LoadPageData();
            return Page();
        }

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToPage("/Account/Login");
        }

        _repository.CreateProject(CreateInput.Name, CreateInput.Description, username);
        return RedirectToPage();
    }

    public IActionResult OnPostUpdateProject()
    {
        ModelState.Clear();
        if (!TryValidateModel(UpdateInput, nameof(UpdateInput)))
        {
            ManageProjectId = UpdateInput.ProjectId > 0 ? UpdateInput.ProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var updated = _repository.UpdateProject(
            UpdateInput.ProjectId,
            username,
            isAdmin,
            UpdateInput.Name,
            UpdateInput.Description,
            out var error);
        if (!updated)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to update project.");
            ManageProjectId = UpdateInput.ProjectId;
            LoadPageData();
            return Page();
        }

        return RedirectToPage(new { manageProjectId = UpdateInput.ProjectId });
    }

    public IActionResult OnPostDeleteProject()
    {
        ModelState.Clear();
        if (!TryValidateModel(DeleteInput, nameof(DeleteInput)))
        {
            ManageProjectId = DeleteInput.ProjectId > 0 ? DeleteInput.ProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var deleted = _repository.DeleteProject(DeleteInput.ProjectId, username, isAdmin, out var error);
        if (!deleted)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to delete project.");
            ManageProjectId = DeleteInput.ProjectId;
            LoadPageData();
            return Page();
        }

        return RedirectToPage();
    }

    [BindProperty]
    [ValidateNever]
    public DeleteProjectInputModel DeleteInput { get; set; } = new();

    public IActionResult OnPostAddMember()
    {
        ModelState.Clear();
        if (!TryValidateModel(MemberInput, nameof(MemberInput)))
        {
            ManageProjectId = MemberInput.ProjectId > 0 ? MemberInput.ProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var saved = _repository.AddOrUpdateMember(
            MemberInput.ProjectId,
            username,
            isAdmin,
            MemberInput.Username,
            MemberInput.Role,
            out var error);
        if (!saved)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to update project membership.");
            ManageProjectId = MemberInput.ProjectId;
            LoadPageData();
            return Page();
        }

        return RedirectToPage(new { manageProjectId = MemberInput.ProjectId });
    }

    private (string? Username, bool IsAdmin) GetActor()
    {
        var username = User.Identity?.Name;
        return (username, User.IsInRole("Admin"));
    }

    private void LoadPageData()
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
        {
            Projects = [];
            return;
        }

        var isAdmin = User.IsInRole("Admin");
        Projects = _repository.GetProjectsForUser(username, isAdmin);

        if (!ManageProjectId.HasValue)
        {
            return;
        }

        var selected = Projects.FirstOrDefault(p => p.Project.Id == ManageProjectId.Value);
        if (selected is null)
        {
            ManageProjectId = null;
            return;
        }

        ManagedProject = selected.Project;
        CanManageSelectedProject = selected.Role == ProjectMemberRole.Maintainer;
        Members = _repository.GetProjectMembers(selected.Project.Id);

        if (CanManageSelectedProject)
        {
            MemberUserOptions = _userAccountService.GetUsers()
                .Select(u => new SelectListItem(u.Username, u.Username))
                .ToList();

            if (UpdateInput.ProjectId == 0)
            {
                UpdateInput = new UpdateProjectInputModel
                {
                    ProjectId = ManagedProject.Id,
                    Name = ManagedProject.Name,
                    Description = ManagedProject.Description
                };
            }

            if (MemberInput.ProjectId == 0)
            {
                MemberInput.ProjectId = ManagedProject.Id;
            }

            if (DeleteInput.ProjectId == 0)
            {
                DeleteInput.ProjectId = ManagedProject.Id;
            }
        }
    }

    public sealed class CreateProjectInputModel
    {
        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(600)]
        public string? Description { get; set; }
    }

    public sealed class UpdateProjectInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(600)]
        public string? Description { get; set; }
    }

    public sealed class DeleteProjectInputModel
    {
        [Required]
        public int ProjectId { get; set; }
    }

    public sealed class AddMemberInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Contributor;
    }
}
