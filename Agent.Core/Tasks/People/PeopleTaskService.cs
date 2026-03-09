using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.LLM;
using Agent.Core.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Tasks.People;

public class PeopleTaskService
{
    private static readonly string[] AllowedTags = ["IT", "transport", "edukacja", "medycyna", "praca z ludźmi", "praca z pojazdami", "praca fizyczna"];

    private static readonly ResponseFormat JobTaggingResponseFormat = new()
                                                                      {
                                                                          Type = "json_schema",
                                                                          JsonSchema = new JsonSchemaDefinition
                                                                                       {
                                                                                           Name = "job_tags",
                                                                                           Strict = true,
                                                                                           Schema = new
                                                                                                    {
                                                                                                        type = "object",
                                                                                                        properties = new
                                                                                                                     {
                                                                                                                         jobs = new
                                                                                                                                {
                                                                                                                                    type = "array",
                                                                                                                                    items = new
                                                                                                                                            {
                                                                                                                                                type = "object",
                                                                                                                                                properties = new
                                                                                                                                                             {
                                                                                                                                                                 index = new { type = "integer" },
                                                                                                                                                                 tags = new
                                                                                                                                                                        {
                                                                                                                                                                            type = "array",
                                                                                                                                                                            items = new
                                                                                                                                                                                    {
                                                                                                                                                                                        type = "string",
                                                                                                                                                                                        @enum =
                                                                                                                                                                                            AllowedTags
                                                                                                                                                                                    }
                                                                                                                                                                        }
                                                                                                                                                             },
                                                                                                                                                required = new[] { "index", "tags" },
                                                                                                                                                additionalProperties = false
                                                                                                                                            }
                                                                                                                                }
                                                                                                                     },
                                                                                                        required = new[] { "jobs" },
                                                                                                        additionalProperties = false
                                                                                                    }
                                                                                       }
                                                                      };

    private readonly ILlmClient _llmClient;

    private readonly ILogger<PeopleTaskService> _logger;

    public PeopleTaskService(ILlmClient llmClient, ILogger<PeopleTaskService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public List<Person> LoadAndFilter(string csvPath)
    {
        var all = ParseCsv(csvPath);
        _logger.LogInformation("Loaded {Total} people from CSV", all.Count);

        var filtered = all.Where(p => p is { Gender: "M", BirthPlace: "Grudziądz", BirthYear: >= 1986 and <= 2006 }).ToList();

        _logger.LogInformation("Filtered to {Count} candidates (male, Grudziądz, born 1986-2006)", filtered.Count);
        return filtered;
    }

    public async Task<List<PersonResult>> TagAndFilterAsync(List<Person> candidates, int batchSize = 0, CancellationToken ct = default)
    {
        // batchSize = 0 means send all at once; otherwise limit to first N for testing
        var toProcess = batchSize > 0 ? candidates.Take(batchSize).ToList() : candidates;

        _logger.LogInformation("Tagging {Count} people (batch mode: {BatchSize})",
                               toProcess.Count,
                               batchSize > 0 ? batchSize.ToString() : "all");

        var taggedJobs = await TagJobsAsync(toProcess, ct);

        var results = new List<PersonResult>();

        for (int i = 0; i < toProcess.Count; i++)
        {
            var person = toProcess[i];
            var tags = taggedJobs.TryGetValue(i, out var t) ? t : [];

            _logger.LogInformation("{Name} {Surname}: [{Tags}]",
                                   person.Name,
                                   person.Surname,
                                   string.Join(", ", tags));

            if (tags.Contains("transport"))
            {
                results.Add(new PersonResult
                            {
                                Name = person.Name,
                                Surname = person.Surname,
                                Gender = person.Gender,
                                Born = person.BirthYear,
                                City = person.BirthPlace,
                                Tags = tags
                            });
            }
        }

        _logger.LogInformation("Found {Count} people with 'transport' tag", results.Count);
        return results;
    }

    private async Task<Dictionary<int, List<string>>> TagJobsAsync(List<Person> people, CancellationToken ct)
    {
        var systemPrompt = $"""
                            You are a job classifier. For each job description provided, assign one or more tags from this list:
                            {string.Join(", ", AllowedTags)}

                            Tag descriptions:
                            - IT: software development, programming, data science, cybersecurity, systems administration
                            - transport: logistics, supply chain, freight, delivery, fleet management, distribution, cargo movement
                            - edukacja: teaching, education, training, tutoring
                            - medycyna: medicine, healthcare, therapy, psychology, nursing, pharmacy
                            - praca z ludźmi: social work, customer service, HR, counselling (people-facing roles not covered by medycyna/edukacja)
                            - praca z pojazdami: vehicle repair, mechanics, automotive engineering, driving
                            - praca fizyczna: construction, manual labour, plumbing, electrical installation, carpentry, masonry

                            Respond with a JSON object containing a "jobs" array. Each element must have:
                            - "index": the 0-based index of the job from the input
                            - "tags": array of matching tags (can be multiple)
                            """;

        var jobsText = string.Join("\n", people.Select((p, i) => $"[{i}] {p.Job}"));

        var messages = new List<ChatMessage> { ChatMessage.System(systemPrompt), ChatMessage.User(jobsText) };

        var response = await _llmClient.ChatAsync(messages, responseFormat: JobTaggingResponseFormat, ct: ct);

        if (response.Error is not null)
            throw new InvalidOperationException($"LLM error during tagging: {response.Error.Message}");

        var content = response.Choices[0].Message.Content ?? "{}";
        _logger.LogDebug("LLM tagging response: {Content}", content);

        return ParseTaggingResponse(content);
    }

    private static Dictionary<int, List<string>> ParseTaggingResponse(string json)
    {
        var result = new Dictionary<int, List<string>>();

        try
        {
            var doc = JsonDocument.Parse(json);
            var jobs = doc.RootElement.GetProperty("jobs");

            foreach (var job in jobs.EnumerateArray())
            {
                var index = job.GetProperty("index").GetInt32();
                var tags = job.GetProperty("tags")
                              .EnumerateArray()
                              .Select(t => t.GetString() ?? string.Empty)
                              .Where(t => !string.IsNullOrEmpty(t))
                              .ToList();
                result[index] = tags;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse LLM tagging response: {ex.Message}\nJSON: {json}", ex);
        }

        return result;
    }

    private static List<Person> ParseCsv(string path)
    {
        var people = new List<Person>();
        var lines = File.ReadAllLines(path);

        // Skip header row
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = SplitCsvLine(line);
            if (fields.Length < 7)
                continue;

            if (!DateOnly.TryParse(fields[3], out var birthDate))
                continue;

            people.Add(new Person
                       {
                           Name = fields[0].Trim(),
                           Surname = fields[1].Trim(),
                           Gender = fields[2].Trim(),
                           BirthDate = birthDate,
                           BirthPlace = fields[4].Trim(),
                           BirthCountry = fields[5].Trim(),
                           Job = fields[6].Trim()
                       });
        }

        return people;
    }

    /// <summary>
    /// Splits a CSV line respecting double-quoted fields that may contain commas.
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // Handle escaped quote ("")
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}

// Internal DTO for deserializing LLM response (used by ParseTaggingResponse via JsonDocument)
file class TaggingResponse
{
    [JsonPropertyName("jobs")]
    public List<TaggedJob> Jobs { get; set; } = [];
}

file class TaggedJob
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
}