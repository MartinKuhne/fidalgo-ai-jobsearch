using Fidalgo.Agent.Agents;
using Fidalgo.Agent.DependencyInjection;
using Fidalgo.Agent.Storage;
using Microsoft.EntityFrameworkCore;

var configFilePath = args.FirstOrDefault(a => a.StartsWith("--config:") || a.StartsWith("-c:"))
    ?.Split(':')[1];

if (!string.IsNullOrEmpty(configFilePath))
{
    using var configHost = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddAgentServices(context.Configuration.GetSection("DatabasePath")?.Get<string>() ?? "jobs.db");
        })
        .Build();

    var handler = configHost.Services.GetRequiredService<ConfigCommandHandler>();
    return await handler.ExecuteAsync(configFilePath);
}

using var appHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddAgentServices(context.Configuration.GetSection("DatabasePath")?.Get<string>() ?? "jobs.db");
        services.AddHostedService<JobSearchAgent>();
    })
    .Build();

await appHost.RunAsync();
return 0;
