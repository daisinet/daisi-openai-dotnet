using System.Text.Json.Serialization;

namespace Daisi.OpenAI.Models;

public class ResponseFormat
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";
}
