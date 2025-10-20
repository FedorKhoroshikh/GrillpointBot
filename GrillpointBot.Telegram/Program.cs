using GrillpointBot.Telegram.BotHandlers;
using Microsoft.Extensions.Configuration;
// Telegram usings
using Telegram.Bot;
using Telegram.Bot.Polling;

namespace GrillpointBot.Telegram;

internal static class Program
{
    private const string AppCfgName = "appsettings.json";

    private static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile($"Config/{AppCfgName}", optional: false)
            .Build();

        string token = config["Telegram:BotToken"];
        var botClient = new TelegramBotClient(token);
        var commandHandler = new CommandHandler(botClient, config);

        var me = await botClient.GetMe();
        Console.WriteLine($"Бот @{me.Username} запущен...");

        var cts = new CancellationTokenSource();
        var receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = []
        };
        
        var updateHandler = new DefaultUpdateHandler(
            async (bot, update, token) =>
                await commandHandler.HandleUpdateAsync(bot, update, token),
            async (bot, exception, source, token) =>
                await CommandHandler.HandleErrorAsync(bot, exception, source, token)
        );
        
        botClient.StartReceiving(
            updateHandler,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );
        
        Console.ReadLine();
        await cts.CancelAsync();
    }
}