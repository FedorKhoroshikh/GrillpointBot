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
            if (update is { Type: UpdateType.Message, Message.Text: not null })
                await messageHandler.HandleMessageAsync(update.Message, ct);
            else if (update.Type == UpdateType.CallbackQuery)
                await callbackHandler.HandleAsync(update.CallbackQuery!, ct);
            
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