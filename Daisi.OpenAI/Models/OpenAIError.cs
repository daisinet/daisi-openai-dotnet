using System.Text.Json.Serialization;

namespace Daisi.OpenAI.Models;

public class OpenAIError
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("param")]
    public string? Param { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class OpenAIErrorWrapper
{
    [JsonPropertyName("error")]
    public OpenAIError Error { get; set; } = new();
}
