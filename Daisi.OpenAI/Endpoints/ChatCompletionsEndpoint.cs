using System.Text;
using System.Text.Json;
using Daisi.OpenAI.Mapping;
using Daisi.OpenAI.Models;
using Daisi.OpenAI.Sessions;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Host;
using Grpc.Core;

namespace Daisi.OpenAI.Endpoints;

public static class ChatCompletionsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/chat/completions", HandleAsync)
            .RequireAuthorization();
    }

    private static async Task HandleAsync(
        HttpContext context,
        InferenceSessionFactory sessionFactory,
        ILogger<Program> logger)
    {
        var request = await context.Request.ReadFromJsonAsync<ChatCompletionRequest>();
        if (request == null)
        {
            await EndpointHelper.WriteValidationError(context, 400,
                "Invalid request body.", "invalid_request_error", "invalid_request");
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            await EndpointHelper.WriteValidationError(context, 400,
                "The 'model' field is required.", "invalid_request_error", "model_required", "model");
            return;
        }

        if (request.Messages.Count == 0)
        {
            await EndpointHelper.WriteValidationError(context, 400,
                "The 'messages' field must contain at least one message.", "invalid_request_error", "messages_required", "messages");
            return;
        }

        if (!EndpointHelper.TryGetCredential(context, out var credential))
        {
            await EndpointHelper.WriteValidationError(context, 401,
                "Authentication credential not found.", "authentication_error", "auth_error");
            return;
        }

        InferenceClient? inferenceClient = null;
        try
        {
            inferenceClient = sessionFactory.Create(credential);

            var createRequest = ChatRequestMapper.ToCreateInferenceRequest(request);
            await inferenceClient.CreateAsync(createRequest);

            var sendRequest = ChatRequestMapper.ToSendInferenceRequest(request);
            var responseStream = inferenceClient.Send(sendRequest);

            var requestId = $"chatcmpl-{Guid.NewGuid():N}";

            if (request.Stream)
            {
                await HandleStreamingResponse(context, responseStream, request.Model, requestId, logger);
            }
            else
            {
                await HandleNonStreamingResponse(context, inferenceClient, responseStream, request.Model, requestId);
            }
        }
        finally
        {
            await EndpointHelper.CloseInferenceClient(inferenceClient, logger);
        }
    }

    private static async Task HandleNonStreamingResponse(
        HttpContext context,
        InferenceClient inferenceClient,
        AsyncServerStreamingCall<SendInferenceResponse> responseStream,
        string model,
        string requestId)
    {
        var contentBuilder = new StringBuilder();
        SendInferenceResponse? lastChunk = null;

        await foreach (var chunk in responseStream.ResponseStream.ReadAllAsync(context.RequestAborted))
        {
            if (ContentExtractor.IsContentChunk(chunk))
            {
                contentBuilder.Append(chunk.Content);
            }
            lastChunk = chunk;
        }

        var cleanedContent = ContentExtractor.CleanResponseContent(contentBuilder.ToString());
        var stats = EndpointHelper.TryGetStats(inferenceClient);
        var response = ChatResponseMapper.ToNonStreamingResponse(model, cleanedContent, stats, requestId);

        // Fallback to inline token counts if Stats() didn't return usage
        if (response.Usage == null && lastChunk != null)
        {
            response.Usage = ChatResponseMapper.MapUsageFromResponse(lastChunk);
        }

        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task HandleStreamingResponse(
        HttpContext context,
        AsyncServerStreamingCall<SendInferenceResponse> responseStream,
        string model,
        string requestId,
        ILogger logger)
    {
        EndpointHelper.PrepareSseResponse(context);

        // Send initial chunk with role
        var initialChunk = ChatResponseMapper.ToStreamingChunk(model, null, null, requestId, includeRole: true);
        await EndpointHelper.WriteSseEvent(context.Response, JsonSerializer.Serialize(initialChunk, EndpointHelper.SseJsonOptions));

        // For streaming, accumulate raw content and post-process
        // since think/response tags can span multiple chunks
        var rawBuilder = new StringBuilder();
        await foreach (var chunk in responseStream.ResponseStream.ReadAllAsync(context.RequestAborted))
        {
            if (ContentExtractor.IsContentChunk(chunk) && !string.IsNullOrEmpty(chunk.Content))
            {
                rawBuilder.Append(chunk.Content);
            }
        }

        var cleanedContent = ContentExtractor.CleanResponseContent(rawBuilder.ToString());
        if (!string.IsNullOrEmpty(cleanedContent))
        {
            var sseChunk = ChatResponseMapper.ToStreamingChunk(model, cleanedContent, null, requestId);
            await EndpointHelper.WriteSseEvent(context.Response, JsonSerializer.Serialize(sseChunk, EndpointHelper.SseJsonOptions));
        }

        // Send final chunk with finish_reason
        var finalChunk = ChatResponseMapper.ToStreamingChunk(model, null, "stop", requestId);
        await EndpointHelper.WriteSseEvent(context.Response, JsonSerializer.Serialize(finalChunk, EndpointHelper.SseJsonOptions));

        // Send [DONE] marker
        await EndpointHelper.WriteSseEvent(context.Response, "[DONE]");
    }
}
