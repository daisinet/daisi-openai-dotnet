using System.Collections.Concurrent;
using Daisi.Protos.V1;
using Daisi.SDK.Clients.V1.Orc;

namespace Daisi.OpenAI.Authentication;

public class DaisiCredentialManager
{
    private readonly ConcurrentDictionary<string, DaisiCredential> _credentials = new();
    private readonly SemaphoreSlim _creationLock = new(1, 1);
    private readonly ILogger<DaisiCredentialManager> _logger;

    public DaisiCredentialManager(ILogger<DaisiCredentialManager> logger)
    {
        _logger = logger;
    }

    public async Task<DaisiCredential> GetOrCreateCredentialAsync(string secretKey)
    {
        if (_credentials.TryGetValue(secretKey, out var existing) && existing.KeyExpiration > DateTime.UtcNow)
        {
            return existing;
        }

        await _creationLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_credentials.TryGetValue(secretKey, out existing) && existing.KeyExpiration > DateTime.UtcNow)
            {
                return existing;
            }

            _logger.LogInformation("Creating new client key for secret key ending in ...{Suffix}",
                secretKey.Length > 4 ? secretKey[^4..] : "****");

            // Use "NOKEY" as the initial client key header value.
            // The secret key is passed in the request body, not the header.
            // The server rejects secret-prefixed keys in the x-daisi-client-key header.
            var keyProvider = new StaticClientKeyProvider("NOKEY");
            var authClientFactory = new AuthClientFactory(keyProvider);
            var authClient = authClientFactory.Create();

            var response = await authClient.CreateClientKeyAsync(new CreateClientKeyRequest
            {
                SecretKey = secretKey
            });

            var expiration = response.KeyExpiration?.ToDateTime() ?? DateTime.UtcNow.AddHours(1);
            var credential = new DaisiCredential(secretKey, response.ClientKey, expiration);

            _credentials[secretKey] = credential;
            _logger.LogInformation("Client key created, expires at {Expiration}", expiration);

            return credential;
        }
        finally
        {
            _creationLock.Release();
        }
    }

    public void RemoveExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _credentials)
        {
            if (kvp.Value.KeyExpiration <= now)
            {
                _credentials.TryRemove(kvp.Key, out _);
                _logger.LogInformation("Removed expired credential for secret key ending in ...{Suffix}",
                    kvp.Key.Length > 4 ? kvp.Key[^4..] : "****");
            }
        }
    }
}
