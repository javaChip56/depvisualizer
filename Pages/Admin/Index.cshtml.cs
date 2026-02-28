using System.ComponentModel.DataAnnotations;
using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dependencies_visualizer.Pages.Admin;

[Authorize(Roles = "Admin")]
public sealed class IndexModel(UserAccountService userAccountService) : PageModel
{
    private readonly UserAccountService _userAccountService = userAccountService;

    [BindProperty]
    public CreateUserInputModel Input { get; set; } = new();

    public IReadOnlyList<UserSummary> Users { get; private set; } = [];

    public void OnGet()
    {
        LoadUsers();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid)
        {
            LoadUsers();
            return Page();
        }

        var created = _userAccountService.CreateUser(
            Input.Username,
            Input.DefaultPassword,
            Input.IsAdmin,
            out var error);
        if (!created)
        {
            ModelState.AddModelError(string.Empty, error ?? "Unable to create user.");
            LoadUsers();
            return Page();
        }

        return RedirectToPage();
    }

    private void LoadUsers()
    {
        Users = _userAccountService.GetUsers();
    }

    public sealed class CreateUserInputModel
    {
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        [Display(Name = "Default Password")]
        public string DefaultPassword { get; set; } = string.Empty;

        [Display(Name = "Grant Admin Role")]
        public bool IsAdmin { get; set; }
    }
}
