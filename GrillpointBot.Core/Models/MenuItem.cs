using System.Text.Json.Serialization;

namespace GrillpointBot.Core.Models;

public sealed class MenuItem
{
    [JsonPropertyName("id")]    public string Id { get; set; } = "";
    [JsonPropertyName("category")]  public string Category { get; set; } = "";
    [JsonPropertyName("name")]  public string Name { get; set; } = "";
    [JsonPropertyName("price")] public decimal Price { get; set; }
    
    [JsonPropertyName("imageKey")] public string? ImageKey { get; set; }
    [JsonPropertyName("imageUrl")] public string? ImageUrl { get; set; }
    
    [JsonPropertyName("weight")] public int?    Weight { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("ingredients")] public List<string>? Ingredients { get; set; }  
}
