using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.LLM;
using Agent.Core.LLM.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Tasks.People;

/// <summary>Delegate used to forward task progress lines to the UI layer without a hard dependency on Agent.</summary>
public delegate void TaskLogWriter(string line);

public class PeopleTaskService
{
    private static readonly string[] AllowedTags =
        ["IT", "transport", "edukacja", "medycyna", "praca z ludźmi", "praca z pojazdami", "praca fizyczna"];

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

    private TaskLogWriter? _logWriter;

    public PeopleTaskService(ILlmClient llmClient, ILogger<PeopleTaskService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    /// <summary>Registers a delegate that receives human-readable progress lines for the UI.</summary>
    public void SetLogWriter(TaskLogWriter writer)
    {
        _logWriter = writer;
    }

    private void Emit(string line)
    {
        _logger.LogInformation("{Line}", line);
        _logWriter?.Invoke(line);
    }

    public List<Person> LoadAndFilter(string csvPath)
    {
        var all = ParseCsv(csvPath);
        Emit($"Loaded {all.Count} people from CSV.");

        var filtered = all.Where(p => p is { Gender: "M", BirthPlace: "Grudziądz", BirthYear: >= 1986 and <= 2006 })
            .ToList();

        Emit($"Filtered to {filtered.Count} candidates (male, Grudziądz, born 1986–2006).");
        return filtered;
    }

    public async Task<List<PersonResult>> TagAndFilterAsync(List<Person> candidates, int batchSize = 0,
        CancellationToken ct = default)
    {
        // batchSize = 0 means send all at once; otherwise limit to first N for testing
        var toProcess = batchSize > 0 ? candidates.Take(batchSize).ToList() : candidates;

        Emit($"Tagging {toProcess.Count} people{(batchSize > 0 ? $" (test: first {batchSize})" : " (all)")}...");

        var taggedJobs = await TagJobsAsync(toProcess, ct);

        var results = new List<PersonResult>();

        for (var i = 0; i < toProcess.Count; i++)
        {
            var person = toProcess[i];
            var tags = taggedJobs.TryGetValue(i, out var t) ? t : [];

            Emit($"  [{i}] {person.Name} {person.Surname}: [{string.Join(", ", tags)}]");

            if (tags.Contains("transport"))
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

        Emit($"Found {results.Count} people with 'transport' tag.");

        var suspectsPath = Path.Combine("files", "suspects.json");
        SaveSuspectsToFile(results, suspectsPath);
        Emit($"// Suspects saved to {suspectsPath}");

        return results;
    }

    /// <summary>
    ///     Serialises the filtered suspects to a JSON file for use by the FindHim task.
    ///     Each entry contains only name, surname, and birthYear.
    /// </summary>
    public void SaveSuspectsToFile(List<PersonResult> suspects, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var entries = suspects.Select(p => new SuspectFileEntry
            { Name = p.Name, Surname = p.Surname, BirthYear = p.Born }).ToList();

        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
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
            throw new InvalidOperationException($"Failed to parse LLM tagging response: {ex.Message}\nJSON: {json}",
                ex);
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
    ///     Splits a CSV line respecting double-quoted fields that may contain commas.
    /// </summary>
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

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
    [JsonPropertyName("jobs")] public List<TaggedJob> Jobs { get; set; } = [];
}

file class TaggedJob
{
    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
}

file class SuspectFileEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("surname")] public string Surname { get; set; } = string.Empty;

    [JsonPropertyName("birthYear")] public int BirthYear { get; set; }
}