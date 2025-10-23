using GrillpointBot.Core.Common;
using GrillpointBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrillpointBot.Telegram.BotHandlers;

public class MessageHandler(ITelegramBotClient bot, IMenuService menuService, DeliveryHandler deliveryHandler)
{
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        var text = msg.Text!;
        var chatId = msg.Chat.Id;
        
        if (await deliveryHandler.HandleDeliveryMessageAsync(msg, ct))
            return; // если сообщение в контексте доставки, остальные команды игнорируются

        switch (text)
        {
            case "/start":
                await bot.SendMessage(chatId,
                    "👋 Добро пожаловать в Grillpoint!\nГорячие сэндвичи, приготовленные с душой.",
                    replyMarkup: MainMenuKeyboard(),
                    cancellationToken: ct);
                break;

            case Constants.MenuCmd:
                await ShowCategoriesAsync(chatId, ct);
                break;
            
            case Constants.AboutUsCmd:
                await ShowAboutUsAsync(chatId, ct);
                break;
            
            case Constants.FeedbackCmd:
                await ShowFeedbackAsync(chatId, ct);
                break;
            
            default:
                await HandleCategoryOrFallback(chatId, text, ct);
                break;
        }
    }

    private async Task ShowCategoriesAsync(long chatId, CancellationToken ct)
    {
        var categories = await menuService.GetCategoriesAsync();
        var buttons = categories.Select(c => new[] { new KeyboardButton(c.Category) }).ToArray();
        var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };

        await bot.SendMessage(chatId, "Выберите категорию:", replyMarkup: markup, cancellationToken: ct);
    }
    
    private async Task ShowAboutUsAsync(long chatId, CancellationToken ct)
    {
        await bot.SendMessage(chatId,
            "ℹ️ Grillpoint — уютное место с горячими сэндвичами и любовью к деталям.",
            cancellationToken: ct);
    }

    private async Task ShowFeedbackAsync(long chatId, CancellationToken ct)
    {
        await bot.SendMessage(chatId,
            "💬 Вы можете оставить отзыв прямо здесь — мы читаем каждый!",
            cancellationToken: ct);
    }

    private async Task HandleCategoryOrFallback(long chatId, string text, CancellationToken ct)
    {
        await menuService.ReloadMenuAsync();
        var items = await menuService.GetItemsByCategoryAsync(text);
        if (items.Any())
        {
            foreach (var item in items)
            {
                var caption = $"🍔 *{item.Name}*\n💰 {item.Price} ₽";
                var markup = new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithCallbackData("Выбрать", $"select_{item.Id}")
                );
                await bot.SendMessage(chatId, caption, parseMode: ParseMode.Markdown,
                    replyMarkup: markup, cancellationToken: ct);
            }
        }
        else
        {
            await bot.SendMessage(chatId, "Пожалуйста, выберите действие из меню 👇",
                replyMarkup: MainMenuKeyboard(), cancellationToken: ct);
        }
    }

    private static ReplyKeyboardMarkup MainMenuKeyboard() =>
        new([
            [new KeyboardButton(Constants.MenuCmd)],
            [new KeyboardButton(Constants.AboutUsCmd), new KeyboardButton(Constants.FeedbackCmd)]
        ]) { ResizeKeyboard = true };
}
