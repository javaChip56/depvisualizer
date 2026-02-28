using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace dependencies_visualizer.Services;

public sealed class UserAccountService
{
    private readonly object _syncRoot = new();
    private readonly PasswordHasher<UserAccount> _passwordHasher = new();
    private readonly Dictionary<string, UserAccount> _users = new(StringComparer.OrdinalIgnoreCase);

    public UserAccountService(IOptions<AuthDefaultsOptions> defaultsOptions)
    {
        var options = defaultsOptions.Value;
        var adminUsername = NormalizeUsername(options.AdminUsername);
        var adminPassword = options.AdminPassword?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(adminUsername))
        {
            adminUsername = "admin";
        }

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = "Admin@123";
        }

        var admin = new UserAccount(adminUsername, isAdmin: true, mustChangePassword: false, passwordHash: string.Empty);
        admin.PasswordHash = _passwordHasher.HashPassword(admin, adminPassword);
        _users[admin.Username] = admin;
    }

    public IReadOnlyList<UserSummary> GetUsers()
    {
        lock (_syncRoot)
        {
            return _users.Values
                .OrderBy(u => u.Username)
                .Select(u => new UserSummary(u.Username, u.IsAdmin, u.MustChangePassword))
                .ToList();
        }
    }

    public bool CreateUser(string username, string defaultPassword, bool isAdmin, out string? error)
    {
        error = null;

        var normalizedUsername = NormalizeUsername(username);
        var password = defaultPassword?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            error = "Username is required.";
            return false;
        }

        if (password.Length < 8)
        {
            error = "Default password must be at least 8 characters.";
            return false;
        }

        lock (_syncRoot)
        {
            if (_users.ContainsKey(normalizedUsername))
            {
                error = "A user with this username already exists.";
                return false;
            }

            var account = new UserAccount(normalizedUsername, isAdmin, mustChangePassword: true, passwordHash: string.Empty);
            account.PasswordHash = _passwordHasher.HashPassword(account, password);
            _users[account.Username] = account;
            return true;
        }
    }

    public bool ValidateCredentials(string username, string password, out AuthenticatedUser? user)
    {
        user = null;
        var normalizedUsername = NormalizeUsername(username);

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return false;
        }

        lock (_syncRoot)
        {
            if (!_users.TryGetValue(normalizedUsername, out var account))
            {
                return false;
            }

            var verification = _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return false;
            }

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            {
                account.PasswordHash = _passwordHasher.HashPassword(account, password);
            }

            user = new AuthenticatedUser(account.Username, account.IsAdmin, account.MustChangePassword);
            return true;
        }
    }

    public bool ChangePassword(string username, string currentPassword, string newPassword, out string? error)
    {
        error = null;
        var normalizedUsername = NormalizeUsername(username);
        var updatedPassword = newPassword?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            error = "Invalid user.";
            return false;
        }

        if (updatedPassword.Length < 8)
        {
            error = "New password must be at least 8 characters.";
            return false;
        }

        lock (_syncRoot)
        {
            if (!_users.TryGetValue(normalizedUsername, out var account))
            {
                error = "User not found.";
                return false;
            }

            var verification = _passwordHasher.VerifyHashedPassword(account, account.PasswordHash, currentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                error = "Current password is incorrect.";
                return false;
            }

            account.PasswordHash = _passwordHasher.HashPassword(account, updatedPassword);
            account.MustChangePassword = false;
            return true;
        }
    }

    public AuthenticatedUser? GetUser(string username)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return null;
        }

        lock (_syncRoot)
        {
            if (!_users.TryGetValue(normalizedUsername, out var account))
            {
                return null;
            }

            return new AuthenticatedUser(account.Username, account.IsAdmin, account.MustChangePassword);
        }
    }

    private static string NormalizeUsername(string? username)
    {
        return username?.Trim() ?? string.Empty;
    }
}

public sealed record AuthenticatedUser(string Username, bool IsAdmin, bool MustChangePassword);

public sealed record UserSummary(string Username, bool IsAdmin, bool MustChangePassword);

internal sealed class UserAccount(string username, bool isAdmin, bool mustChangePassword, string passwordHash)
{
    public string Username { get; } = username;

    public bool IsAdmin { get; } = isAdmin;

    public bool MustChangePassword { get; set; } = mustChangePassword;

    public string PasswordHash { get; set; } = passwordHash;
}
