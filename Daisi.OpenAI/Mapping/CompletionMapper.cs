using Daisi.OpenAI.Models;
using Daisi.Protos.V1;
using Daisi.SDK.Extensions;

namespace Daisi.OpenAI.Mapping;

public static class CompletionMapper
{
    public static CreateInferenceRequest ToCreateInferenceRequest(CompletionRequest request)
    {
        return new CreateInferenceRequest
        {
            ModelName = request.Model,
            ThinkLevel = ThinkLevels.Basic
        };
    }

    public static SendInferenceRequest ToSendInferenceRequest(CompletionRequest request)
    {
        var sendRequest = SendInferenceRequest.CreateDefault();

        sendRequest.Text = request.GetPromptAsString();

        if (request.Temperature.HasValue)
            sendRequest.Temperature = request.Temperature.Value;
        if (request.TopP.HasValue)
            sendRequest.TopP = request.TopP.Value;
        if (request.MaxTokens.HasValue)
            sendRequest.MaxTokens = request.MaxTokens.Value;
        if (request.Seed.HasValue)
            sendRequest.Seed = request.Seed.Value;
        if (request.FrequencyPenalty.HasValue)
            sendRequest.FrequencyPenalty = request.FrequencyPenalty.Value;
        if (request.PresencePenalty.HasValue)
            sendRequest.PresencePenalty = request.PresencePenalty.Value;

        return sendRequest;
    }

    public static CompletionResponse ToNonStreamingResponse(
        string model,
        string accumulatedText,
        InferenceStatsResponse? stats,
        string id)
    {
        return new CompletionResponse
        {
            Id = id,
            Object = "text_completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices =
            [
                new CompletionChoice
                {
                    Text = accumulatedText,
                    Index = 0,
                    FinishReason = "stop"
                }
            ],
            Usage = ChatResponseMapper.MapUsage(stats)
        };
    }

    public static CompletionResponse ToStreamingChunk(
        string model,
        string? textDelta,
        string? finishReason,
        string id)
    {
        return new CompletionResponse
        {
            Id = id,
            Object = "text_completion",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = model,
            Choices =
            [
                new CompletionChoice
                {
                    Text = textDelta ?? string.Empty,
                    Index = 0,
                    FinishReason = finishReason
                }
            ]
        };
    }
}
