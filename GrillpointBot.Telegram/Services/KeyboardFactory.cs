using GrillpointBot.Core.Common;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.BotHandlers;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrillpointBot.Telegram.Services;

public static class Kb
{
    public static InlineKeyboardMarkup MainInline() => new([
        [ InlineKeyboardButton.WithCallbackData("🍔 Меню", CallbackPrefixes.MainMenu) ],
        [
            InlineKeyboardButton.WithCallbackData("ℹ️ О нас", CallbackPrefixes.AboutUs),
            InlineKeyboardButton.WithCallbackData("⭐ Отзывы", CallbackPrefixes.Feedback)
        ]
    ]);

    public static InlineKeyboardMarkup BackToMain() => new([
        [ InlineKeyboardButton.WithCallbackData("⬅️ Назад", CallbackPrefixes.BackToMain) ]
    ]);
    
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
            [InlineKeyboardButton.WithCallbackData("➕ Добавить", $"{CallbackPrefixes.AddStart}{itemId}")]
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
    
    public static InlineKeyboardMarkup SkipComment() => new ([
        [
            InlineKeyboardButton.WithCallbackData("Пропустить", CallbackPrefixes.SkipComment)
        ]
    ]);

    public static InlineKeyboardMarkup SaveOrEdit(string savePrefix, string editPrefix) => new ([
        [
            InlineKeyboardButton.WithCallbackData("✅ Сохранить", savePrefix),
            InlineKeyboardButton.WithCallbackData("✏️ Изменить", editPrefix)
        ]
    ]);
    
    public static InlineKeyboardMarkup CheckoutMethod() => new([
        [ InlineKeyboardButton.WithCallbackData("🚚 Доставка", $"{CallbackPrefixes.CheckoutMethodDelivery}") ],
        [ InlineKeyboardButton.WithCallbackData("🏃 Самовывоз", $"{CallbackPrefixes.CheckoutMethodPickup}") ]
    ]);

    public static InlineKeyboardMarkup ConfirmOrder() => new(
        [
            [InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"{CallbackPrefixes.CheckoutConfirm}")],
            
            [
                InlineKeyboardButton.WithCallbackData("❌ Отменить",   $"{CallbackPrefixes.CheckoutCancel}"),
                InlineKeyboardButton.WithCallbackData("✏️ Изменить",   $"{CallbackPrefixes.CheckoutEdit}")
            ]
        ]
    );
 
    public static InlineKeyboardMarkup DateKb()
    {
        var today = DateTime.Today;
        var days = Enumerable.Range(0, 3)
            .Select(i => today.AddDays(i))
            .Select(d => InlineKeyboardButton.WithCallbackData(
                d.ToString("dd.MM"), $"time:date:{d:yyyyMMdd}")).ToArray();
        return new InlineKeyboardMarkup([
            [days[0], days[1], days[2]]
        ]);
    }

    public static InlineKeyboardMarkup TimeKb(DateTime date)
    {
        var start = date.Date.AddHours(9);      // с 9:00
        var end = date.AddHours(21);            // до 21:00
        var now = DateTime.Now.AddMinutes(20);  // Время - с текущего +20 минут
        var times = new List<List<InlineKeyboardButton>>();
        var row = new List<InlineKeyboardButton>();
        for (var t = start; t < end; t = t.AddMinutes(20))
        {
            if (date.Date == now.Date && t < now) continue;
            row.Add(InlineKeyboardButton.WithCallbackData(
                t.ToString("HH:mm"), $"{CallbackPrefixes.ChooseTime}:{t:yyyyMMddHHmm}"));
            if (row.Count == 3)
            {
                times.Add(row);
                row = [];
            }
        }
        if (row.Count > 0) times.Add(row);
        return new InlineKeyboardMarkup(times);
    }
    
    public static ReplyKeyboardMarkup Phone() => new([
            [
                new KeyboardButton("📞 Отправить телефон"){ RequestContact = true }
            ]
        ])
    { ResizeKeyboard = true, OneTimeKeyboard = true };
}