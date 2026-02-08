using Daisi.OpenAI.Authentication;
using Daisi.OpenAI.Mapping;
using Daisi.OpenAI.Models;
using Daisi.SDK.Clients.V1.Orc;

namespace Daisi.OpenAI.Endpoints;

public static class ModelsEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/models", HandleListAsync)
            .RequireAuthorization();

        app.MapGet("/v1/models/{modelId}", HandleGetAsync)
            .RequireAuthorization();
    }

    private static async Task HandleListAsync(HttpContext context)
    {
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

        var keyProvider = new StaticClientKeyProvider(credential.ClientKey);
        var modelClientFactory = new ModelClientFactory(keyProvider);
        var modelClient = modelClientFactory.Create();

        var modelsResponse = modelClient.GetRequiredModels();
        var models = ModelMapper.ToModelList(modelsResponse.Models);

        await context.Response.WriteAsJsonAsync(new ModelListResponse
        {
            Object = "list",
            Data = models
        });
    }

    private static async Task HandleGetAsync(HttpContext context, string modelId)
    {
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

        var keyProvider = new StaticClientKeyProvider(credential.ClientKey);
        var modelClientFactory = new ModelClientFactory(keyProvider);
        var modelClient = modelClientFactory.Create();

        var modelsResponse = modelClient.GetRequiredModels();
        var model = modelsResponse.Models
            .Where(m => m.Enabled)
            .FirstOrDefault(m => m.Name.Equals(modelId, StringComparison.OrdinalIgnoreCase));

        if (model == null)
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsJsonAsync(new OpenAIErrorWrapper
            {
                Error = new OpenAIError
                {
                    Message = $"The model '{modelId}' does not exist.",
                    Type = "invalid_request_error",
                    Param = "model",
                    Code = "model_not_found"
                }
            });
            return;
        }

        await context.Response.WriteAsJsonAsync(ModelMapper.ToModelObject(model));
    }
}
