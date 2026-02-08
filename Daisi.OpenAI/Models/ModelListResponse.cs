using System.Text.Json.Serialization;

namespace Daisi.OpenAI.Models;

public class ModelListResponse
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = "list";

    [JsonPropertyName("data")]
    public List<ModelObject> Data { get; set; } = [];
}
