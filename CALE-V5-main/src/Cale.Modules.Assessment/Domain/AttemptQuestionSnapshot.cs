using System.Text.Json;
using System.Text.Json.Serialization;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Assessment.Domain;

public sealed class AttemptQuestionSnapshot
{
    public string Text { get; init; } = "";
    public string Type { get; init; } = "";
    public string? ImageUrl { get; init; }
    public string? Explanation { get; init; }
    public string? Topic { get; init; }
    public int BlockId { get; init; }
    public IReadOnlyList<AttemptOptionSnapshot> Options { get; init; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AttemptQuestionSnapshot FromQuestion(
        Question question,
        IReadOnlyList<QuestionOption> presentedOptions) =>
        new()
        {
            Text = question.Text,
            Type = question.Type,
            ImageUrl = question.ImageUrl,
            Explanation = question.Explanation,
            Topic = question.Topic,
            BlockId = question.BlockId,
            Options = presentedOptions
                .Select(o => new AttemptOptionSnapshot(
                    o.Id,
                    o.Text,
                    o.IsCorrect,
                    o.ImageUrl))
                .ToList()
        };

    public static string Serialize(AttemptQuestionSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, JsonOptions);

    public static AttemptQuestionSnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() is "{}" or "null")
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AttemptQuestionSnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public AttemptOptionSnapshot? FindOption(int optionId) =>
        Options.FirstOrDefault(o => o.Id == optionId);

    public AttemptOptionSnapshot? CorrectOption() =>
        Options.FirstOrDefault(o => o.IsCorrect);
}

public sealed record AttemptOptionSnapshot(
    int Id,
    string Text,
    bool IsCorrect,
    string? ImageUrl);
