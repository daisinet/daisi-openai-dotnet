using Daisi.OpenAI.Authentication;

namespace Daisi.OpenAI.Sessions;

public class SessionCleanupService : BackgroundService
{
    private readonly DaisiCredentialManager _credentialManager;
    private readonly ILogger<SessionCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public SessionCleanupService(DaisiCredentialManager credentialManager, ILogger<SessionCleanupService> logger)
    {
        _credentialManager = credentialManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);

            try
            {
                _credentialManager.RemoveExpired();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during credential cleanup");
            }
        }
    }
}
