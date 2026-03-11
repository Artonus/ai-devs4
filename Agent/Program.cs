using Agent.Components;
using Agent.Core.Agent;
using Agent.Core.Configuration;
using Agent.Core.LLM;
using Agent.Core.Tasks.People;
using Agent.Core.Tools;
using Agent.Core.Tools.Implementations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

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
       .AddSingleton<TaskLogService>()
       .AddSingleton<PeopleTaskService>()
       .AddSingleton<HubClient>();

builder.Services
       .AddRazorComponents()
       .AddInteractiveServerComponents();

var app = builder.Build();

var options = app.Services.GetRequiredService<AgentOptions>();

if (string.IsNullOrWhiteSpace(options.ApiKey))
    app.Logger.LogWarning("ApiKey is not set. Add it to appsettings.json under Agent:ApiKey");

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();