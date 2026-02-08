using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Daisi.OpenAI.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Daisi.OpenAI.Authentication;

public class BearerTokenAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DaisiBearerToken";
    public const string CredentialItemKey = "DaisiCredential";

    private readonly DaisiCredentialManager _credentialManager;

    public BearerTokenAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DaisiCredentialManager credentialManager)
        : base(options, logger, encoder)
    {
        _credentialManager = credentialManager;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Missing or invalid Authorization header. Expected: Bearer <secret-key>");
        }

        var secretKey = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(secretKey))
        {
            return AuthenticateResult.Fail("Bearer token is empty.");
        }

        try
        {
            var credential = await _credentialManager.GetOrCreateCredentialAsync(secretKey);
            Context.Items[CredentialItemKey] = credential;

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, credential.ClientKey) };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Authentication failed for secret key");
            return AuthenticateResult.Fail($"Authentication failed: {ex.Message}");
        }
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.ContentType = "application/json";
        var error = new OpenAIErrorWrapper
        {
            Error = new OpenAIError
            {
                Message = "Incorrect API key provided. You can find your API key (DAISI Secret Key) in the DAISI dashboard.",
                Type = "invalid_request_error",
                Code = "invalid_api_key"
            }
        };
        await Response.WriteAsync(JsonSerializer.Serialize(error));
    }
}
