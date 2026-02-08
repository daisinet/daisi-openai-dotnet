using Daisi.OpenAI.Authentication;
using Daisi.OpenAI.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace Daisi.OpenAI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDaisiOpenAI(this IServiceCollection services)
    {
        services.AddSingleton<DaisiCredentialManager>();
        services.AddSingleton<InferenceSessionFactory>();
        services.AddHostedService<SessionCleanupService>();

        services.AddAuthentication(BearerTokenAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthHandler>(
                BearerTokenAuthHandler.SchemeName, null);

        services.AddAuthorization();

        return services;
    }
}
