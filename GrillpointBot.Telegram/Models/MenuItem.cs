using System.Text.Json.Serialization;

namespace GrillpointBot.Telegram.Models;

public class MenuCategory
{
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("items")]    public List<MenuItem> Items { get; set; } = new();
}

public class MenuItem
{
    [JsonPropertyName("id")]    public string Id { get; set; } = "";
    [JsonPropertyName("name")]  public string Name { get; set; } = "";
    [JsonPropertyName("price")] public decimal Price { get; set; }
}
