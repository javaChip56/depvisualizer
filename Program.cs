using dependencies_visualizer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("node-shapes.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile("node-types.json", optional: false, reloadOnChange: true);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<DependencyRepository>();
builder.Services.AddSingleton<NodeShapeResolver>();
builder.Services.AddSingleton<NodeTypeCatalog>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();
app.Run();
