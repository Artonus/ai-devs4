using Agent.Core.Agent;
using Agent.Core.Configuration;
using Agent.Core.LLM;
using Agent.Core.Tasks.FindHim;
using Agent.Core.Tasks.People;
using Agent.Core.Tools;
using Agent.Core.Tools.Implementations;
using Agent.UI.Components;
using Agent.UI.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>();

builder.Services
       .Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName))
       .AddSingleton(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value)
       .AddSingleton<ILlmClient, OpenRouterClient>()
       .AddSingleton<PeopleTaskService>()
       .AddSingleton<HubClient>()
       .AddSingleton<ToolRegistry>(sp =>
                                   {
                                       var opts = sp.GetRequiredService<AgentOptions>();
                                       var peopleTask = sp.GetRequiredService<PeopleTaskService>();
                                       var registry = new ToolRegistry();
                                       registry.Register(new FileReadTool());
                                        registry.Register(new LoadSuspectsTool(peopleTask, opts));
                                       registry.Register(new HubApiQueryTool(opts));
                                       registry.Register(new CalculateDistanceTool());
                                       registry.Register(new SubmitFindHimAnswerTool(opts));
                                       return registry;
                                   })
       .AddSingleton<AgentRunner>()
       .AddSingleton<TaskLogService>()
       .AddSingleton<FindHimTaskService>();

// Add services to the container.
builder.Services.AddRazorComponents()
       .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();