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
    public CreateEntityInputModel CreateEntityInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public UpdateProjectInputModel UpdateInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public DeleteProjectInputModel DeleteInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public AddProjectMemberInputModel ProjectMemberInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public RemoveProjectMemberInputModel RemoveProjectMemberInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public UpdateSubProjectInputModel UpdateSubProjectInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public DeleteSubProjectInputModel DeleteSubProjectInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public AddSubProjectMemberInputModel SubProjectMemberInput { get; set; } = new();

    [BindProperty]
    [ValidateNever]
    public RemoveSubProjectMemberInputModel RemoveSubProjectMemberInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? ManageProjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ManageSubProjectId { get; set; }

    public IReadOnlyList<ProjectAccessSummary> Projects { get; private set; } = [];
    public IReadOnlyDictionary<int, IReadOnlyList<SubProjectAccessSummary>> SubProjectsByProjectId { get; private set; }
        = new Dictionary<int, IReadOnlyList<SubProjectAccessSummary>>();
    public IEnumerable<SelectListItem> ProjectOptions { get; private set; } = [];

    public Project? ManagedProject { get; private set; }
    public bool CanManageSelectedProject { get; private set; }
    public IReadOnlyList<ProjectMemberSummary> ProjectMembers { get; private set; } = [];

    public IReadOnlyList<SubProjectAccessSummary> SubProjects { get; private set; } = [];
    public SubProject? ManagedSubProject { get; private set; }
    public bool CanEditSelectedSubProject { get; private set; }
    public bool CanManageSelectedSubProject { get; private set; }
    public IReadOnlyList<SubProjectMemberSummary> SubProjectMembers { get; private set; } = [];

    public IEnumerable<SelectListItem> MemberUserOptions { get; private set; } = [];

    public void OnGet()
    {
        LoadPageData();
    }

    public IActionResult OnPostCreateEntity()
    {
        ModelState.Clear();
        if (!TryValidateModel(CreateEntityInput, nameof(CreateEntityInput)))
        {
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        if (CreateEntityInput.EntityType == CreateEntityType.Project)
        {
            var createdProject = _repository.CreateProject(CreateEntityInput.Name, CreateEntityInput.Description, username);
            return RedirectToPage(new { manageProjectId = createdProject.Id });
        }

        if (!CreateEntityInput.ParentProjectId.HasValue || CreateEntityInput.ParentProjectId.Value <= 0)
        {
            ModelState.AddModelError($"{nameof(CreateEntityInput)}.{nameof(CreateEntityInput.ParentProjectId)}", "Parent project is required for a sub project.");
            LoadPageData();
            return Page();
        }

        var createdSubProject = _repository.CreateSubProject(
            CreateEntityInput.ParentProjectId.Value,
            username,
            isAdmin,
            CreateEntityInput.Name,
            CreateEntityInput.Description,
            out var subProject,
            out var createError);
        if (!createdSubProject || subProject is null)
        {
            ModelState.AddModelError(string.Empty, createError ?? "Unable to create sub project.");
            ManageProjectId = CreateEntityInput.ParentProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            CreateEntityInput.ParentProjectId.Value,
            username,
            "Create",
            "SubProject",
            $"Created sub project '{subProject.Name}'.");

        return RedirectToPage(new { manageProjectId = subProject.ProjectId, manageSubProjectId = subProject.Id });
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

    public IActionResult OnPostAddProjectMember()
    {
        ModelState.Clear();
        if (!TryValidateModel(ProjectMemberInput, nameof(ProjectMemberInput)))
        {
            ManageProjectId = ProjectMemberInput.ProjectId > 0 ? ProjectMemberInput.ProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var saved = _repository.AddOrUpdateMember(
            ProjectMemberInput.ProjectId,
            username,
            isAdmin,
            ProjectMemberInput.Username,
            ProjectMemberRole.Maintainer,
            out var error);
        if (!saved)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to add project member.");
            ManageProjectId = ProjectMemberInput.ProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            ProjectMemberInput.ProjectId,
            username,
            "Update",
            "ProjectMember",
            $"Added/updated project member '{ProjectMemberInput.Username}' as Maintainer.");

        return RedirectToPage(new { manageProjectId = ProjectMemberInput.ProjectId });
    }

    public IActionResult OnPostRemoveProjectMember()
    {
        ModelState.Clear();
        if (!TryValidateModel(RemoveProjectMemberInput, nameof(RemoveProjectMemberInput)))
        {
            ManageProjectId = RemoveProjectMemberInput.ProjectId > 0 ? RemoveProjectMemberInput.ProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var removed = _repository.RemoveMember(
            RemoveProjectMemberInput.ProjectId,
            username,
            isAdmin,
            RemoveProjectMemberInput.Username,
            out var error);
        if (!removed)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to remove project member.");
            ManageProjectId = RemoveProjectMemberInput.ProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            RemoveProjectMemberInput.ProjectId,
            username,
            "Delete",
            "ProjectMember",
            $"Removed project member '{RemoveProjectMemberInput.Username}'.");

        return RedirectToPage(new { manageProjectId = RemoveProjectMemberInput.ProjectId });
    }

    public IActionResult OnPostUpdateSubProject()
    {
        ModelState.Clear();
        if (!TryValidateModel(UpdateSubProjectInput, nameof(UpdateSubProjectInput)))
        {
            ManageProjectId = UpdateSubProjectInput.ProjectId > 0 ? UpdateSubProjectInput.ProjectId : null;
            ManageSubProjectId = UpdateSubProjectInput.SubProjectId > 0 ? UpdateSubProjectInput.SubProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var updated = _repository.UpdateSubProject(
            UpdateSubProjectInput.ProjectId,
            UpdateSubProjectInput.SubProjectId,
            username,
            isAdmin,
            UpdateSubProjectInput.Name,
            UpdateSubProjectInput.Description,
            out var error);
        if (!updated)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to update sub project.");
            ManageProjectId = UpdateSubProjectInput.ProjectId;
            ManageSubProjectId = UpdateSubProjectInput.SubProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            UpdateSubProjectInput.ProjectId,
            username,
            "Update",
            "SubProject",
            $"Updated sub project '{UpdateSubProjectInput.Name}'.");

        return RedirectToPage(new { manageProjectId = UpdateSubProjectInput.ProjectId, manageSubProjectId = UpdateSubProjectInput.SubProjectId });
    }

    public IActionResult OnPostDeleteSubProject()
    {
        ModelState.Clear();
        if (!TryValidateModel(DeleteSubProjectInput, nameof(DeleteSubProjectInput)))
        {
            ManageProjectId = DeleteSubProjectInput.ProjectId > 0 ? DeleteSubProjectInput.ProjectId : null;
            ManageSubProjectId = DeleteSubProjectInput.SubProjectId > 0 ? DeleteSubProjectInput.SubProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var deletingSubProject = _repository.GetSubProjectById(DeleteSubProjectInput.SubProjectId);
        var deleted = _repository.DeleteSubProject(DeleteSubProjectInput.ProjectId, DeleteSubProjectInput.SubProjectId, username, isAdmin, out var error);
        if (!deleted)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to delete sub project.");
            ManageProjectId = DeleteSubProjectInput.ProjectId;
            ManageSubProjectId = DeleteSubProjectInput.SubProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            DeleteSubProjectInput.ProjectId,
            username,
            "Delete",
            "SubProject",
            $"Deleted sub project '{deletingSubProject?.Name ?? DeleteSubProjectInput.SubProjectId.ToString()}'.");

        return RedirectToPage(new { manageProjectId = DeleteSubProjectInput.ProjectId });
    }

    public IActionResult OnPostAddSubProjectMember()
    {
        ModelState.Clear();
        if (!TryValidateModel(SubProjectMemberInput, nameof(SubProjectMemberInput)))
        {
            ManageProjectId = SubProjectMemberInput.ProjectId > 0 ? SubProjectMemberInput.ProjectId : null;
            ManageSubProjectId = SubProjectMemberInput.SubProjectId > 0 ? SubProjectMemberInput.SubProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var saved = _repository.AddOrUpdateSubProjectMember(
            SubProjectMemberInput.ProjectId,
            SubProjectMemberInput.SubProjectId,
            username,
            isAdmin,
            SubProjectMemberInput.Username,
            SubProjectMemberInput.Role,
            out var error);
        if (!saved)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to update sub project membership.");
            ManageProjectId = SubProjectMemberInput.ProjectId;
            ManageSubProjectId = SubProjectMemberInput.SubProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            SubProjectMemberInput.ProjectId,
            username,
            "Update",
            "SubProjectMember",
            $"Set member '{SubProjectMemberInput.Username}' as {SubProjectMemberInput.Role} in sub project {SubProjectMemberInput.SubProjectId}.");

        return RedirectToPage(new { manageProjectId = SubProjectMemberInput.ProjectId, manageSubProjectId = SubProjectMemberInput.SubProjectId });
    }

    public IActionResult OnPostRemoveSubProjectMember()
    {
        ModelState.Clear();
        if (!TryValidateModel(RemoveSubProjectMemberInput, nameof(RemoveSubProjectMemberInput)))
        {
            ManageProjectId = RemoveSubProjectMemberInput.ProjectId > 0 ? RemoveSubProjectMemberInput.ProjectId : null;
            ManageSubProjectId = RemoveSubProjectMemberInput.SubProjectId > 0 ? RemoveSubProjectMemberInput.SubProjectId : null;
            LoadPageData();
            return Page();
        }

        var (username, isAdmin) = GetActor();
        if (username is null)
        {
            return RedirectToPage("/Account/Login");
        }

        var removed = _repository.RemoveSubProjectMember(
            RemoveSubProjectMemberInput.ProjectId,
            RemoveSubProjectMemberInput.SubProjectId,
            username,
            isAdmin,
            RemoveSubProjectMemberInput.Username,
            out var error);
        if (!removed)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to remove sub project member.");
            ManageProjectId = RemoveSubProjectMemberInput.ProjectId;
            ManageSubProjectId = RemoveSubProjectMemberInput.SubProjectId;
            LoadPageData();
            return Page();
        }

        _repository.AddAuditEntry(
            RemoveSubProjectMemberInput.ProjectId,
            username,
            "Delete",
            "SubProjectMember",
            $"Removed member '{RemoveSubProjectMemberInput.Username}' from sub project {RemoveSubProjectMemberInput.SubProjectId}.");

        return RedirectToPage(new { manageProjectId = RemoveSubProjectMemberInput.ProjectId, manageSubProjectId = RemoveSubProjectMemberInput.SubProjectId });
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
        SubProjectsByProjectId = Projects.ToDictionary(
            p => p.Project.Id,
            p => _repository.GetSubProjectsForProject(p.Project.Id, username, isAdmin),
            comparer: EqualityComparer<int>.Default);

        ProjectOptions = Projects
            .Where(p => _repository.UserCanManageProject(p.Project.Id, username, isAdmin))
            .Select(p => new SelectListItem(p.Project.Name, p.Project.Id.ToString()))
            .ToList();

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
        CanManageSelectedProject = _repository.UserCanManageProject(selected.Project.Id, username, isAdmin);
        SubProjects = _repository.GetSubProjectsForProject(selected.Project.Id, username, isAdmin);

        if (CanManageSelectedProject)
        {
            ProjectMembers = _repository.GetProjectMembers(selected.Project.Id);

            MemberUserOptions = _userAccountService.GetUsers()
                .Where(u => !u.IsSuspended)
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

            if (DeleteInput.ProjectId == 0)
            {
                DeleteInput.ProjectId = ManagedProject.Id;
            }

            if (ProjectMemberInput.ProjectId == 0)
            {
                ProjectMemberInput.ProjectId = ManagedProject.Id;
            }

            if (RemoveProjectMemberInput.ProjectId == 0)
            {
                RemoveProjectMemberInput.ProjectId = ManagedProject.Id;
            }
        }

        if (ManageSubProjectId.HasValue)
        {
            var selectedSubProject = SubProjects.FirstOrDefault(sp => sp.SubProject.Id == ManageSubProjectId.Value);
            if (selectedSubProject is null)
            {
                ManageSubProjectId = null;
                return;
            }

            ManagedSubProject = selectedSubProject.SubProject;
            CanEditSelectedSubProject = _repository.UserCanAccessSubProject(ManagedSubProject.Id, username, isAdmin);
            CanManageSelectedSubProject = _repository.UserCanManageSubProject(ManagedSubProject.Id, username, isAdmin);

            if (CanEditSelectedSubProject)
            {
                UpdateSubProjectInput = new UpdateSubProjectInputModel
                {
                    ProjectId = ManagedProject.Id,
                    SubProjectId = ManagedSubProject.Id,
                    Name = ManagedSubProject.Name,
                    Description = ManagedSubProject.Description
                };
            }

            if (CanManageSelectedSubProject)
            {
                SubProjectMembers = _repository.GetSubProjectMembers(ManagedSubProject.Id, username, isAdmin);

                if (DeleteSubProjectInput.SubProjectId == 0)
                {
                    DeleteSubProjectInput.ProjectId = ManagedProject.Id;
                    DeleteSubProjectInput.SubProjectId = ManagedSubProject.Id;
                }

                if (SubProjectMemberInput.SubProjectId == 0)
                {
                    SubProjectMemberInput.ProjectId = ManagedProject.Id;
                    SubProjectMemberInput.SubProjectId = ManagedSubProject.Id;
                }

                if (RemoveSubProjectMemberInput.SubProjectId == 0)
                {
                    RemoveSubProjectMemberInput.ProjectId = ManagedProject.Id;
                    RemoveSubProjectMemberInput.SubProjectId = ManagedSubProject.Id;
                }
            }
        }
    }

    public enum CreateEntityType
    {
        Project = 0,
        SubProject = 1
    }

    public sealed class CreateEntityInputModel
    {
        [Required]
        public CreateEntityType EntityType { get; set; } = CreateEntityType.Project;

        [Display(Name = "Parent Project")]
        public int? ParentProjectId { get; set; }

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

    public sealed class AddProjectMemberInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
    }

    public sealed class RemoveProjectMemberInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
    }

    public sealed class UpdateSubProjectInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int SubProjectId { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(600)]
        public string? Description { get; set; }
    }

    public sealed class DeleteSubProjectInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int SubProjectId { get; set; }
    }

    public sealed class AddSubProjectMemberInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int SubProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Contributor;
    }

    public sealed class RemoveSubProjectMemberInputModel
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int SubProjectId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;
    }
}
