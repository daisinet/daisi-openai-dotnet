using System.Text.Json;
using Daisi.OpenAI.Mapping;
using Daisi.OpenAI.Models;
using Daisi.OpenAI.Sessions;
using Daisi.SDK.Clients.V1.Host;

namespace Daisi.OpenAI.Endpoints;

public static class CompletionsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/completions", HandleAsync)
            .RequireAuthorization();
    }

    private static async Task HandleAsync(
        HttpContext context,
        InferenceSessionFactory sessionFactory,
        ILogger<Program> logger)
    {
        var request = await context.Request.ReadFromJsonAsync<CompletionRequest>();
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

            var createRequest = CompletionMapper.ToCreateInferenceRequest(request);
            await inferenceClient.CreateAsync(createRequest);

            var sendRequest = CompletionMapper.ToSendInferenceRequest(request);
            var responseStream = inferenceClient.Send(sendRequest);

            var requestId = $"cmpl-{Guid.NewGuid():N}";

            if (request.Stream)
            {
                await HandleStreamingResponse(context, responseStream, request.Model, requestId);
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
        Grpc.Core.AsyncServerStreamingCall<Daisi.Protos.V1.SendInferenceResponse> responseStream,
        string model,
        string requestId)
    {
        var cleanedText = await EndpointHelper.ReadAndCleanContent(responseStream, context.RequestAborted);
        var stats = EndpointHelper.TryGetStats(inferenceClient);
        var response = CompletionMapper.ToNonStreamingResponse(model, cleanedText, stats, requestId);
        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task HandleStreamingResponse(
        HttpContext context,
        Grpc.Core.AsyncServerStreamingCall<Daisi.Protos.V1.SendInferenceResponse> responseStream,
        string model,
        string requestId)
    {
        EndpointHelper.PrepareSseResponse(context);

        var cleanedText = await EndpointHelper.ReadAndCleanContent(responseStream, context.RequestAborted);
        if (!string.IsNullOrEmpty(cleanedText))
        {
            var sseChunk = CompletionMapper.ToStreamingChunk(model, cleanedText, null, requestId);
            await EndpointHelper.WriteSseEvent(context.Response, JsonSerializer.Serialize(sseChunk, EndpointHelper.SseJsonOptions));
        }

        // Send final chunk with finish_reason
        var finalChunk = CompletionMapper.ToStreamingChunk(model, null, "stop", requestId);
        await EndpointHelper.WriteSseEvent(context.Response, JsonSerializer.Serialize(finalChunk, EndpointHelper.SseJsonOptions));

        // Send [DONE] marker
        await EndpointHelper.WriteSseEvent(context.Response, "[DONE]");
    }
}
