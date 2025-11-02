using System.Text.Json;
using System.Text.Json.Serialization;
using GrillpointBot.Core.Common;

namespace GrillpointBot.Core.Config;

public class AppSettings
{
    [JsonPropertyName("BotToken")]
    public string BotToken { get; set; }
    
    [JsonPropertyName("AdminChatId")]
    public string AdminChatId { get; set; }

    public long AdminChatIdNum => long.TryParse(AdminChatId, out var aci) ? aci : 0;

    public static AppSettings? LoadConfig()
    {
        var json = File.ReadAllText(Constants.CfgPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return settings;
    }
}