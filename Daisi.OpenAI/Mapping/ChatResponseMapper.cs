using Daisi.OpenAI.Models;
using Daisi.Protos.V1;

namespace Daisi.OpenAI.Mapping;

public static class ChatResponseMapper
{
    public static ChatCompletionResponse ToNonStreamingResponse(
        string model,
        string accumulatedContent,
        InferenceStatsResponse? stats,
        string id)
    {
        return new ChatCompletionResponse
        {
            Id = id,
            Object = "chat.completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices =
            [
                new ChatCompletionChoice
                {
                    Index = 0,
                    Message = new ChatMessage
                    {
                        Role = "assistant",
                        Content = accumulatedContent.TrimEnd()
                    },
                    FinishReason = "stop"
                }
            ],
            Usage = MapUsage(stats)
        };
    }

    public static ChatCompletionChunk ToStreamingChunk(
        string model,
        string? contentDelta,
        string? finishReason,
        string id,
        bool includeRole = false)
    {
        return new ChatCompletionChunk
        {
            Id = id,
            Object = "chat.completion.chunk",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices =
            [
                new ChatCompletionChunkChoice
                {
                    Index = 0,
                    Delta = new ChatCompletionDelta
                    {
                        Role = includeRole ? "assistant" : null,
                        Content = contentDelta
                    },
                    FinishReason = finishReason
                }
            ]
        };
    }

    public static OpenAIUsage? MapUsage(InferenceStatsResponse? stats)
    {
        if (stats == null) return null;

        // DAISI reports token counts at the session level
        // We approximate prompt vs completion split
        return new OpenAIUsage
        {
            PromptTokens = stats.SessionTokenCount - stats.LastMessageTokenCount,
            CompletionTokens = stats.LastMessageTokenCount,
            TotalTokens = stats.SessionTokenCount
        };
    }

    /// <summary>
    /// Map usage from inline token counts in the final SendInferenceResponse chunk.
    /// Used when Stats() call is not available (e.g., streaming responses).
    /// </summary>
    public static OpenAIUsage? MapUsageFromResponse(SendInferenceResponse? lastChunk)
    {
        if (lastChunk == null || lastChunk.SessionTokenCount == 0)
            return null;

        return new OpenAIUsage
        {
            PromptTokens = lastChunk.SessionTokenCount - lastChunk.MessageTokenCount,
            CompletionTokens = lastChunk.MessageTokenCount,
            TotalTokens = lastChunk.SessionTokenCount
        };
    }
}
