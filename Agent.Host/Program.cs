using Agent.Core.Configuration;
using Agent.Core.LLM;
using Agent.Core.Agent;
using Agent.Core.Tasks.People;
using Agent.Core.Tools;
using Agent.Core.Tools.Implementations;
using Agent.Host.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services
    .Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName))
    .AddSingleton(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value)
    .AddSingleton<ILlmClient, OpenRouterClient>()
    .AddSingleton<ToolRegistry>(sp =>
    {
        var registry = new ToolRegistry();
        registry.Register(new FileReadTool());
        return registry;
    })
    .AddSingleton<AgentRunner>()
    .AddSingleton<PeopleTaskService>()
    .AddSingleton<HubClient>()
    .AddHostedService<ReplService>();

var host = builder.Build();

var options = host.Services.GetRequiredService<AgentOptions>();

if (string.IsNullOrWhiteSpace(options.ApiKey))
{
    Console.WriteLine("[ERROR] ApiKey is not set. Add it to appsettings.json under Agent:ApiKey.");
    return;
}

Console.WriteLine($"Agent ready. Model: {options.Model}");
Console.WriteLine("Type your message and press Enter. Type 'exit' to quit.\n");

await host.RunAsync();
