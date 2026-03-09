using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Tasks.People;

public class HubClient
{
    private readonly AgentOptions _options;
    private readonly ILogger<HubClient> _logger;

    public HubClient(AgentOptions options, ILogger<HubClient> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<HubResponse> SubmitPeopleAsync(List<PersonResult> people, CancellationToken ct = default)
    {
        var payload = new HubRequest
        {
            ApiKey = _options.AiDevsKey,
            Task = "people",
            Answer = people
        };

        _logger.LogInformation("Submitting {Count} people to {Url}/verify", people.Count, _options.HubBaseUrl);
        _logger.LogDebug("Payload: {Json}", JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        try
        {
            var response = await (_options.HubBaseUrl + "/verify")
                .PostJsonAsync(payload, cancellationToken: ct)
                .ReceiveJson<HubResponse>();

            _logger.LogInformation("Hub response: {Message} (code: {Code})", response.Message, response.Code);
            return response;
        }
        catch (FlurlHttpException ex)
        {
            var errorBody = await ex.GetResponseStringAsync();
            throw new InvalidOperationException($"Hub API error ({ex.StatusCode}): {errorBody}", ex);
        }
    }
}

public class HubRequest
{
    [JsonPropertyName("apikey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("answer")]
    public List<PersonResult> Answer { get; set; } = [];
}

public class HubResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
