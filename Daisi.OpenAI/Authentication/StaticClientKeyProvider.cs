using Daisi.SDK.Interfaces.Authentication;

namespace Daisi.OpenAI.Authentication;

public class StaticClientKeyProvider(string clientKey) : IClientKeyProvider
{
    public string GetClientKey() => clientKey;
}
