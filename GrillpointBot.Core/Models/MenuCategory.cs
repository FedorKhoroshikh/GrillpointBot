using System.Text.Json.Serialization;

namespace GrillpointBot.Core.Models;

public class MenuCategory
{
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("items")]    public List<MenuItem> Items { get; set; } = new();
}