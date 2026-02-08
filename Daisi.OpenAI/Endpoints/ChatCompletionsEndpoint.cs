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

        if (request.Messages.Count == 0)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = "The 'messages' field must contain at least one message.",
                    Type = "invalid_request_error",
                    Param = "messages",
                    Code = "messages_required"
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
        var contentBuilder = new StringBuilder();

        await foreach (var chunk in responseStream.ResponseStream.ReadAllAsync(context.RequestAborted))
        {
            if (ContentExtractor.IsContentChunk(chunk))
            {
                contentBuilder.Append(chunk.Content);
            }
        }

        var cleanedContent = ContentExtractor.CleanResponseContent(contentBuilder.ToString());

        InferenceStatsResponse? stats = null;
        try
        {
            stats = inferenceClient.Stats(new InferenceStatsRequest());
        }
        catch
        {
            // Stats are optional; don't fail the response
        }

        var response = ChatResponseMapper.ToNonStreamingResponse(model, cleanedContent, stats, requestId);
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

        // Send initial chunk with role
        var initialChunk = ChatResponseMapper.ToStreamingChunk(model, null, null, requestId, includeRole: true);
        await WriteSseEvent(context.Response, JsonSerializer.Serialize(initialChunk, jsonOptions));

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
            await WriteSseEvent(context.Response, JsonSerializer.Serialize(sseChunk, jsonOptions));
        }

        // Send final chunk with finish_reason
        var finalChunk = ChatResponseMapper.ToStreamingChunk(model, null, "stop", requestId);
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
