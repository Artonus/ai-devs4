using Agent.Core.Agent;
using Agent.Core.Configuration;
using Agent.Core.Hub;
using Agent.Core.LLM;
using Agent.Core.Tasks.FindHim;
using Agent.Core.Tasks.People;
using Agent.Core.Tasks.SendIt;
using Agent.Core.Tools;
using Agent.Core.Tools.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Agent.Core.Extensions;

public static class AgentCoreServiceExtensions
{
    public static IServiceCollection AddAgentCore(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .Configure<AgentOptions>(configuration.GetSection(AgentOptions.SectionName))
            .AddSingleton(sp => sp.GetRequiredService<IOptions<AgentOptions>>().Value)
            .AddSingleton<ILlmClient, OpenRouterClient>()
            .AddSingleton<HubClient>()
            .AddSingleton<PeopleTaskService>()
            .AddSingleton<ToolRegistry>(sp =>
            {
                var opts = sp.GetRequiredService<AgentOptions>();
                var hub = sp.GetRequiredService<HubClient>();
                var peopleTask = sp.GetRequiredService<PeopleTaskService>();
                var llmClient = sp.GetRequiredService<ILlmClient>();
                var registry = new ToolRegistry();
                registry.Register(new FileReadTool());
                registry.Register(new LoadSuspectsTool(peopleTask, opts));
                registry.Register(new HubApiQueryTool(opts));
                registry.Register(new CalculateDistanceTool());
                registry.Register(new SubmitFindHimAnswerTool(opts));
                registry.Register(new FetchUrlTool(llmClient));
                registry.Register(new AnalyzeImageTool(llmClient));
                registry.Register(new SubmitAnswerTool(hub));
                return registry;
            })
            .AddSingleton<AgentRunner>()
            .AddSingleton<FindHimTaskService>()
            .AddSingleton<SendItTaskService>();

        return services;
    }
}
