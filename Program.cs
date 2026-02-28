using dependencies_visualizer.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("node-shapes.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("node-types.json", optional: false, reloadOnChange: true);

builder.Services.Configure<AuthDefaultsOptions>(builder.Configuration.GetSection(AuthDefaultsOptions.SectionName));
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddSingleton<DependencyRepository>();
builder.Services.AddSingleton<NodeShapeResolver>();
builder.Services.AddSingleton<NodeTypeCatalog>();
builder.Services.AddSingleton<UserAccountService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var mustChangePassword = string.Equals(
        context.User.FindFirst(AuthClaimTypes.MustChangePassword)?.Value,
        bool.TrueString,
        StringComparison.OrdinalIgnoreCase);
    var requestPath = context.Request.Path;
    var isStaticAssetRequest = Path.HasExtension(requestPath.Value);

    if (context.User.Identity?.IsAuthenticated == true &&
        mustChangePassword &&
        !isStaticAssetRequest &&
        !requestPath.StartsWithSegments("/Account/ChangePassword", StringComparison.OrdinalIgnoreCase) &&
        !requestPath.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Account/ChangePassword");
        return;
    }

    await next();
});
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
