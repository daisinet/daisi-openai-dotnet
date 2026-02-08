using System.Text;
using System.Text.Json;
using Daisi.OpenAI.Authentication;
using Daisi.OpenAI.Mapping;
using Daisi.OpenAI.Models;
using Daisi.OpenAI.Sessions;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Host;
using Grpc.Core;

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
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = "Invalid request body.",
                    Type = "invalid_request_error",
                    Code = "invalid_request"
                }
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = "The 'model' field is required.",
                    Type = "invalid_request_error",
                    Param = "model",
                    Code = "model_required"
                }
            });
            return;
        }

        var credential = context.Items[BearerTokenAuthHandler.CredentialItemKey] as DaisiCredential;
        if (credential == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = "Authentication credential not found.",
                    Type = "authentication_error",
                    Code = "auth_error"
                }
            });
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
                await HandleStreamingResponse(context, responseStream, request.Model, requestId, logger);
            }
            else
            {
                await HandleNonStreamingResponse(context, inferenceClient, responseStream, request.Model, requestId);
            }
        }
        finally
        {
            if (inferenceClient != null)
            {
                try
                {
                    await inferenceClient.CloseAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error closing inference client");
                }
            }
        }
    }

    private static async Task HandleNonStreamingResponse(
        HttpContext context,
        InferenceClient inferenceClient,
        AsyncServerStreamingCall<SendInferenceResponse> responseStream,
        string model,
        string requestId)
    {
        var textBuilder = new StringBuilder();

        await foreach (var chunk in responseStream.ResponseStream.ReadAllAsync(context.RequestAborted))
        {
            if (ContentExtractor.IsContentChunk(chunk))
            {
                textBuilder.Append(chunk.Content);
            }
        }

        var cleanedText = ContentExtractor.CleanResponseContent(textBuilder.ToString());

        InferenceStatsResponse? stats = null;
        try
        {
            stats = inferenceClient.Stats(new InferenceStatsRequest());
        }
        catch
        {
            // Stats are optional
        }

        var response = CompletionMapper.ToNonStreamingResponse(model, cleanedText, stats, requestId);
        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task HandleStreamingResponse(
        HttpContext context,
        AsyncServerStreamingCall<SendInferenceResponse> responseStream,
        string model,
        string requestId,
        ILogger logger)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        var jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        var rawBuilder = new StringBuilder();
        await foreach (var chunk in responseStream.ResponseStream.ReadAllAsync(context.RequestAborted))
        {
            if (ContentExtractor.IsContentChunk(chunk) && !string.IsNullOrEmpty(chunk.Content))
            {
                rawBuilder.Append(chunk.Content);
            }
        }

        var cleanedText = ContentExtractor.CleanResponseContent(rawBuilder.ToString());
        if (!string.IsNullOrEmpty(cleanedText))
        {
            var sseChunk = CompletionMapper.ToStreamingChunk(model, cleanedText, null, requestId);
            await WriteSseEvent(context.Response, JsonSerializer.Serialize(sseChunk, jsonOptions));
        }

        // Send final chunk with finish_reason
        var finalChunk = CompletionMapper.ToStreamingChunk(model, null, "stop", requestId);
        await WriteSseEvent(context.Response, JsonSerializer.Serialize(finalChunk, jsonOptions));

        // Send [DONE] marker
        await WriteSseEvent(context.Response, "[DONE]");
    }

    private static async Task WriteSseEvent(HttpResponse response, string data)
    {
        await response.WriteAsync($"data: {data}\n\n");
        await response.Body.FlushAsync();
    }
}
