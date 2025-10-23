using System.Text.Json.Serialization;

namespace GrillpointBot.Core.Models;

public class MenuItem
{
    [JsonPropertyName("id")]    public string Id { get; set; } = "";
    [JsonPropertyName("name")]  public string Name { get; set; } = "";
    [JsonPropertyName("price")] public decimal Price { get; set; }
}
