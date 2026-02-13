using System.Text;
using System.Text.Json;
using Daisi.OpenAI.Authentication;
using Daisi.OpenAI.Mapping;
using Daisi.OpenAI.Models;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Host;
using Grpc.Core;

namespace Daisi.OpenAI.Endpoints;

internal static class EndpointHelper
{
    internal static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    internal static async Task WriteSseEvent(HttpResponse response, string data)
    {
        await response.WriteAsync($"data: {data}\n\n");
        await response.Body.FlushAsync();
    }

    internal static void PrepareSseResponse(HttpContext context)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";
    }

    internal static bool TryGetCredential(HttpContext context, out DaisiCredential credential)
    {
        credential = context.Items[BearerTokenAuthHandler.CredentialItemKey] as DaisiCredential;
        return credential != null;
    }

    internal static async Task WriteValidationError(HttpContext context, int statusCode, string message,
        string type, string code, string? param = null)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
        {
            Error = new OpenAIError
            {
                Message = message,
                Type = type,
                Param = param,
                Code = code
            }
        });
    }

    internal static async Task<string> ReadAndCleanContent(
        AsyncServerStreamingCall<SendInferenceResponse> responseStream,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        await foreach (var chunk in responseStream.ResponseStream.ReadAllAsync(cancellationToken))
        {
            if (ContentExtractor.IsContentChunk(chunk))
            {
                builder.Append(chunk.Content);
            }
        }
        return ContentExtractor.CleanResponseContent(builder.ToString());
    }

    internal static InferenceStatsResponse? TryGetStats(InferenceClient inferenceClient)
    {
        try
        {
            return inferenceClient.Stats(new InferenceStatsRequest());
        }
        catch
        {
            // Stats are optional; don't fail the response
            return null;
        }
    }

    internal static async Task CloseInferenceClient(InferenceClient? inferenceClient, ILogger logger)
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
