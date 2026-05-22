using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Fidalgo.Web.Components;
using Fidalgo.Web.Services;
using Fidalgo.Shared.Storage;
using MudBlazor;
using MudBlazor.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMudServices();

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "fidalgo", "jobs.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
var optionsBuilder = new DbContextOptionsBuilder<JobDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");
builder.Services.AddScoped(sp => optionsBuilder.Options);
builder.Services.AddScoped<JobRepository>();
builder.Services.AddScoped<IJobsService, JobsService>();
builder.Services.AddScoped<ITenantService, TenantService>();

await builder.Build().RunAsync();
