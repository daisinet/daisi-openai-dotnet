using Daisi.OpenAI.Endpoints;

namespace Daisi.OpenAI.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapOpenAIEndpoints(this IEndpointRouteBuilder app)
    {
        ChatCompletionsEndpoint.Map(app);
        CompletionsEndpoint.Map(app);
        ModelsEndpoint.Map(app);
        EmbeddingsEndpoint.Map(app);
        return app;
    }
}
