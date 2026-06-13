using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Components;
using Muddi.DramaMeter.Blazor.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

// Register DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DramaMeter")
	?? throw new InvalidOperationException("Connection string 'DramaMeter' not found.");
builder.Services.AddDbContext<DramaMeterDbContext>(options =>
	options.UseNpgsql(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

// Apply pending migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DramaMeterDbContext>();
    db.Database.Migrate();
}

app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

app.Run();