namespace dependencies_visualizer.Services;

public sealed class AuthDefaultsOptions
{
    public const string SectionName = "AuthDefaults";

    public string AdminUsername { get; set; } = "admin";

    public string AdminPassword { get; set; } = "Admin@123";
}
