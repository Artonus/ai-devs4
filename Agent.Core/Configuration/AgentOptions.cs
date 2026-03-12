namespace Agent.Core.Configuration;

public class AgentOptions
{
    public const string SectionName = "Agent";

    public string Model { get; set; } = "anthropic/claude-3.5-sonnet";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    public string SystemPrompt { get; set; } = "You are a helpful assistant.";

    public string ApiKey { get; set; } = string.Empty;

    public string AiDevsKey { get; set; } = string.Empty;

    public string HubBaseUrl { get; set; } = "https://hub.ag3nts.org";

    public string PeopleCsvPath { get; set; } = "files/people.csv";

    public string SuspectsFilePath { get; set; } = "files/suspects.json";

    public string SuspectLocationsFilePath { get; set; } = "files/suspect_locations.json";
}