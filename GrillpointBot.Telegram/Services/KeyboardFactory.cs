using GrillpointBot.Core.Common;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.BotHandlers;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrillpointBot.Telegram.Services;

public static class Kb
{
    public static ReplyKeyboardMarkup Main() =>
        new([
            [new KeyboardButton(Constants.MenuCmd)],
            [new KeyboardButton(Constants.AboutUsCmd), new KeyboardButton(Constants.FeedbackCmd)]
        ]) { ResizeKeyboard = true };
    
    public static InlineKeyboardMarkup Restart() => new([
        [
            InlineKeyboardButton.WithCallbackData("✅ Да, начать заново", "session:restart"),
            InlineKeyboardButton.WithCallbackData("❌ Нет, продолжить", "session:keep")
        ]
    ]);

    public static InlineKeyboardMarkup Categories(IEnumerable<MenuCategory> categories)
    {
        var buttons = categories.Chunk(2).Select(group => group
            .Select(c => 
                InlineKeyboardButton.WithCallbackData($"{c.Category}", $"{CallbackPrefixes.Category}:{c.Category}"))
            .ToList())
            .ToList();

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup CardAdd(string itemId) => new(
        [
            [InlineKeyboardButton.WithCallbackData("➕ Добавить", $"{CallbackPrefixes.AddStart}{itemId}")],
            [InlineKeyboardButton.WithCallbackData("🧺 Корзина", CallbackPrefixes.OpenCart)]
        ]
    );
    
    public static InlineKeyboardMarkup CardQty(string itemId, int qty) => new(
        [
            [
                InlineKeyboardButton.WithCallbackData("➖", $"{CallbackPrefixes.AddDec}{itemId}"),
                InlineKeyboardButton.WithCallbackData(qty.ToString(), "noop"),
                InlineKeyboardButton.WithCallbackData("➕", $"{CallbackPrefixes.AddInc}{itemId}")
            ],
            
            [InlineKeyboardButton.WithCallbackData("🧺 Корзина", CallbackPrefixes.OpenCart)]
        ]
    );

    public static InlineKeyboardMarkup CartSummary() => new( 
        [
            [InlineKeyboardButton.WithCallbackData("Изменить", CallbackPrefixes.CartEdit)],   // просто вернёт к категориям
            [InlineKeyboardButton.WithCallbackData("Продолжить", CallbackPrefixes.CartContinue)]
        ]
    );
    
    public static InlineKeyboardMarkup CartSumInactive() => new(
        [
            [InlineKeyboardButton.WithCallbackData("Изменить", "noop_disabled")],
            [InlineKeyboardButton.WithCallbackData("Продолжить", "noop_disabled")]
        ]
    );

    public static InlineKeyboardMarkup Comment() => new ([
        [
            InlineKeyboardButton.WithCallbackData("✅ Сохранить", CallbackPrefixes.SaveComment),
            InlineKeyboardButton.WithCallbackData("✏️ Изменить", CallbackPrefixes.EditComment)
        ]
    ]);
}