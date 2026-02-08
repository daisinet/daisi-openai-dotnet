namespace Daisi.OpenAI.Authentication;

public record DaisiCredential(string SecretKey, string ClientKey, DateTime KeyExpiration);
