using System.Text.Json;
using Daisi.OpenAI.Models;
using Grpc.Core;

namespace Daisi.OpenAI.Middleware;

public class OpenAIErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OpenAIErrorMiddleware> _logger;

    public OpenAIErrorMiddleware(RequestDelegate next, ILogger<OpenAIErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (RpcException rpcEx)
        {
            _logger.LogError(rpcEx, "gRPC error: {Status} - {Detail}", rpcEx.StatusCode, rpcEx.Status.Detail);

            if (context.Response.HasStarted)
                return;

            var (httpStatus, errorType, errorCode) = MapGrpcStatus(rpcEx.StatusCode);
            context.Response.StatusCode = httpStatus;
            context.Response.ContentType = "application/json";

            var error = new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = rpcEx.Status.Detail ?? rpcEx.Message,
                    Type = errorType,
                    Code = errorCode
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
        catch (OperationCanceledException)
        {
            // Client disconnected, no response needed
            _logger.LogDebug("Request was cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            if (context.Response.HasStarted)
                return;

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var error = new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = "An internal server error occurred.",
                    Type = "server_error",
                    Code = "internal_error"
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error));
        }
    }

    private static (int HttpStatus, string ErrorType, string ErrorCode) MapGrpcStatus(StatusCode grpcStatus)
    {
        return grpcStatus switch
        {
            StatusCode.Unauthenticated => (401, "authentication_error", "invalid_api_key"),
            StatusCode.PermissionDenied => (403, "permission_error", "permission_denied"),
            StatusCode.NotFound => (404, "invalid_request_error", "not_found"),
            StatusCode.InvalidArgument => (400, "invalid_request_error", "invalid_argument"),
            StatusCode.ResourceExhausted => (429, "rate_limit_error", "rate_limit_exceeded"),
            StatusCode.Unavailable => (503, "server_error", "service_unavailable"),
            StatusCode.DeadlineExceeded => (504, "server_error", "timeout"),
            _ => (500, "server_error", "internal_error")
        };
    }
}
