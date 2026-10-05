using System.Text.Json;

namespace Application.Jobs.JobTypes;

public static class TextInput
{
    public static IReadOnlyDictionary<string, string[]>? Validate(JsonElement input, int maxChars)
    {
        if (input.ValueKind != JsonValueKind.Object ||
            !input.TryGetProperty("text", out var t) || t.ValueKind != JsonValueKind.String)
            return Error("input.text", "A string property 'text' is required.");

        var text = t.GetString()!;
        if (string.IsNullOrWhiteSpace(text)) return Error("input.text", "Text must not be empty.");
        if (text.Length > maxChars) return Error("input.text", $"Text must be at most {maxChars} characters.");
        return null;
    }

    public static string Read(string inputJson)
    {
        using var doc = JsonDocument.Parse(inputJson);
        return doc.RootElement.GetProperty("text").GetString()!;
    }

    private static Dictionary<string, string[]> Error(string key, string msg) => new() { [key] = [msg] };
}