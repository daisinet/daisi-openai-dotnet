using Daisi.OpenAI.Authentication;
using Daisi.SDK.Clients.V1.Host;
using Daisi.SDK.Clients.V1.Orc;
using Daisi.SDK.Clients.V1.SessionManagers;

namespace Daisi.OpenAI.Sessions;

public class InferenceSessionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public InferenceSessionFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public InferenceClient Create(DaisiCredential credential)
    {
        var keyProvider = new StaticClientKeyProvider(credential.ClientKey);
        var sessionClientFactory = new SessionClientFactory(keyProvider);
        var sessionManager = new InferenceSessionManager(
            sessionClientFactory,
            keyProvider,
            _loggerFactory.CreateLogger<InferenceSessionManager>());
        var inferenceClientFactory = new InferenceClientFactory(sessionManager);
        return inferenceClientFactory.Create();
    }
}
