using Agent.Core.Agent;
using Agent.Core.Tasks.People;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Host.Services;

public class ReplService : BackgroundService
{
    private readonly AgentRunner _agent;

    private readonly PeopleTaskService _peopleTask;

    private readonly HubClient _hubClient;

    private readonly IHostApplicationLifetime _lifetime;

    private readonly ILogger<ReplService> _logger;

    private const string PeopleCsvPath = "/home/bbratus@atsi.zab/works/ai-devs4/files/people.csv";

    public ReplService(AgentRunner agent, PeopleTaskService peopleTask, HubClient hubClient, IHostApplicationLifetime lifetime, ILogger<ReplService> logger)
    {
        _agent = agent;
        _peopleTask = peopleTask;
        _hubClient = hubClient;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield so other hosted services can start before we block on stdin

        Console.WriteLine("Commands: 'people test' | 'people run' | 'exit'");
        Console.WriteLine();

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.Write("> ");
            var input = Console.ReadLine();

            if (input is null || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _lifetime.StopApplication();
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
                continue;

            try
            {
                if (input.Equals("people test", StringComparison.OrdinalIgnoreCase))
                {
                    await RunPeopleTaskAsync(batchSize: 3, submit: false, stoppingToken);
                }
                else if (input.Equals("people run", StringComparison.OrdinalIgnoreCase))
                {
                    await RunPeopleTaskAsync(batchSize: 0, submit: true, stoppingToken);
                }
                else
                {
                    var response = await _agent.RunAsync(input, stoppingToken);
                    Console.WriteLine();
                    Console.WriteLine(response);
                    Console.WriteLine();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing input");
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.WriteLine();
            }
        }
    }

    private async Task RunPeopleTaskAsync(int batchSize, bool submit, CancellationToken ct)
    {
        Console.WriteLine(batchSize > 0
                              ? $"\n[People] Running test mode (first {batchSize} candidates)..."
                              : "\n[People] Running full mode...");

        var candidates = _peopleTask.LoadAndFilter(PeopleCsvPath);
        Console.WriteLine($"[People] {candidates.Count} candidates after static filter.\n");

        var results = await _peopleTask.TagAndFilterAsync(candidates, batchSize, ct);

        Console.WriteLine($"\n[People] {results.Count} people tagged with 'transport':");
        foreach (var r in results)
            Console.WriteLine($"  {r.Name} {r.Surname} ({r.Born}, {r.City}) — [{string.Join(", ", r.Tags)}]");

        if (submit)
        {
            Console.WriteLine("\n[People] Submitting to hub...");
            var response = await _hubClient.SubmitPeopleAsync(results, ct);
            Console.WriteLine($"[People] Hub response ({response.Code}): {response.Message}");
        }
        else
        {
            Console.WriteLine("\n[People] Test mode — not submitting. Use 'people run' to submit.");
        }

        Console.WriteLine();
    }
}