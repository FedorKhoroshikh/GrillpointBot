using System.Text.Json;
using System.Text.Json.Serialization;
using GrillpointBot.Core.Common;

namespace GrillpointBot.Core.Config;

public class AppSettings
{
    [JsonPropertyName("Telegram:BotToken")]
    public string BotToken { get; set; }
    
    [JsonPropertyName("Telegram:AdminChatId")]
    public long AdminChatId { get; set; }

    public static AppSettings? LoadConfig()
    {
        var json = File.ReadAllText(Constants.CfgPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return settings;
    }
}