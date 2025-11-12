using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GrillpointBot.Telegram.BotHandlers;

public class UpdateRouter(
    ITelegramBotClient bot, 
    MessageHandler messageHandler, 
    CallbackHandler callbackHandler)
{
    private readonly CancellationTokenSource _cts = new();
    
    public void Start()
    {
        var options = new ReceiverOptions { AllowedUpdates = [] };
        bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, options, _cts.Token);
        Console.WriteLine("🤖 GrillpointBot started. Waiting for updates...");
    }
    
    private async Task HandleUpdateAsync(ITelegramBotClient _bot, Update update, CancellationToken ct)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await messageHandler.HandleMessageAsync(update.Message, ct);
                    break;
                case UpdateType.CallbackQuery:
                    await callbackHandler.HandleCallbackAsync(update.CallbackQuery!, ct);
                    break;
            }

            Console.WriteLine($"[Router] Update received: {update.Type}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Router Error] {e.Message}");
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, 
        HandleErrorSource source, CancellationToken ct)
    {
        Console.WriteLine($"Error ({source}): {ex.Message}");
        return Task.CompletedTask;
    }
}