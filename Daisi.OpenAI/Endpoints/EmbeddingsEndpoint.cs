using Daisi.OpenAI.Models;

namespace Daisi.OpenAI.Endpoints;

public static class EmbeddingsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/embeddings", HandleAsync)
            .RequireAuthorization();
    }

    private static async Task HandleAsync(HttpContext context)
    {
        context.Response.StatusCode = 501;
        await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
        {
            Error = new OpenAIError
            {
                Message = "Embeddings are not supported by the DAISI network at this time.",
                Type = "invalid_request_error",
                Code = "not_implemented"
            }
        });
    }
}
