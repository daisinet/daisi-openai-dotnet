using System.Text.RegularExpressions;
using Daisi.Protos.V1;

namespace Daisi.OpenAI.Mapping;

public static partial class ContentExtractor
{
    private static readonly string[] AntiPrompts = ["User:", "User:\n", "\n\n\n", "###"];

    /// <summary>
    /// Determines if a response chunk contains content that should be included in the output.
    /// </summary>
    public static bool IsContentChunk(SendInferenceResponse chunk)
    {
        return chunk.Type is InferenceResponseTypes.Text or InferenceResponseTypes.ToolContent;
    }

    /// <summary>
    /// Cleans accumulated response text by removing think tags, response tags,
    /// and trailing anti-prompt sequences.
    /// </summary>
    public static string CleanResponseContent(string rawContent)
    {
        if (string.IsNullOrEmpty(rawContent))
            return string.Empty;

        // Remove <think>...</think> blocks
        var cleaned = ThinkTagRegex().Replace(rawContent, "");

        // Remove <response> and </response> tags (keep inner content)
        cleaned = cleaned.Replace("<response>", "").Replace("</response>", "");

        // Trim trailing anti-prompt sequences
        cleaned = cleaned.TrimEnd();
        foreach (var antiPrompt in AntiPrompts)
        {
            var trimmed = antiPrompt.Trim();
            if (!string.IsNullOrEmpty(trimmed) && cleaned.EndsWith(trimmed, StringComparison.Ordinal))
            {
                cleaned = cleaned[..^trimmed.Length].TrimEnd();
            }
        }

        return cleaned;
    }

    [GeneratedRegex(@"<think>[\s\S]*?</think>", RegexOptions.Compiled)]
    private static partial Regex ThinkTagRegex();
}
