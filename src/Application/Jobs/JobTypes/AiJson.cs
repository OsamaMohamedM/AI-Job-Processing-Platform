namespace Application.Jobs.JobTypes;

using Application.Common;
using System.Text.Json;

public static class AiJson
{
    public static string ReadString(string aiText, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(StripFences(aiText));
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(property, out var p) &&
                p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString()))
                return p.GetString()!.Trim();
        }
        catch (JsonException) { }
        throw new InvalidAiResponseException($"Missing or invalid '{property}' in AI response.");
    }

    private static string StripFences(string s)
    {
        s = s.Trim();
        if (!s.StartsWith("```")) return s;
        var firstNewline = s.IndexOf('\n');
        var body = firstNewline >= 0 ? s[(firstNewline + 1)..] : s;
        return body.TrimEnd('`', '\n', '\r', ' ');
    }
}