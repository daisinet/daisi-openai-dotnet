using System.Text;
using Daisi.OpenAI.Models;
using Daisi.Protos.V1;
using Daisi.SDK.Extensions;

namespace Daisi.OpenAI.Mapping;

public static class ChatRequestMapper
{
    public static CreateInferenceRequest ToCreateInferenceRequest(ChatCompletionRequest request)
    {
        var createRequest = new CreateInferenceRequest
        {
            ModelName = request.Model,
            ThinkLevel = request.Tools is { Count: > 0 } ? ThinkLevels.Skilled : ThinkLevels.Basic
        };

        // Extract system message as initialization prompt
        var systemMessage = request.Messages.FirstOrDefault(m =>
            m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
        if (systemMessage != null)
        {
            createRequest.InitializationPrompt = systemMessage.GetContentAsString();
        }

        return createRequest;
    }

    public static SendInferenceRequest ToSendInferenceRequest(ChatCompletionRequest request)
    {
        var sendRequest = SendInferenceRequest.CreateDefault();

        sendRequest.Text = FormatConversationHistory(request.Messages);

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

    public static string FormatConversationHistory(List<ChatMessage> messages)
    {
        var sb = new StringBuilder();

        foreach (var message in messages)
        {
            if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                continue;

            var prefix = message.Role.ToLowerInvariant() switch
            {
                "user" => "User:",
                "assistant" => "Assistant:",
                "tool" => "Tool:",
                _ => $"{message.Role}:"
            };

            sb.AppendLine($"{prefix} {message.GetContentAsString()}");
        }

        // Append the final "Assistant:" prompt so the model continues
        sb.Append("Assistant:");

        return sb.ToString();
    }
}
