using System.Text.Json;
using GrillpointBot.Telegram.Models;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;

namespace GrillpointBot.Telegram.BotHandlers;

public class CommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly string? _menuPath;
    private List<MenuCategory> _menu = new();
    
    private const string OrdersPath = "orders.json";
    private readonly long _adminChatId;
    
    private readonly Dictionary<long, Order> _pendingOrders = new();
    
    public CommandHandler(TelegramBotClient bot, IConfigurationRoot config)
    {
        _bot = bot;
        _menuPath = config["Menu:Path"];
        _adminChatId = long.Parse(config["Telegram:AdminChatId"]!);
        LoadMenu();
    }

#region Info displaying methods

    private async Task ShowCategories(long chatId, CancellationToken ct)
    {
        var buttons = _menu.Select(c => new[] { new KeyboardButton(c.Category) }).ToArray();
        var markup = new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true };
        
        Console.WriteLine($"[Menu] Sending categories: {_menu.Count}");
        foreach (var c in _menu)
            Console.WriteLine($"[Menu]  -> {c.Category}");

        
        await _bot.SendMessage(chatId, "Выберите категорию:", replyMarkup: markup, cancellationToken: ct);
    }

    private async Task ShowItems(long chatId, MenuCategory category, CancellationToken ct)
    {
        foreach (var item in category.Items)
        {
            var text = $"🍔 *{item.Name}*\n💰 {item.Price} ₽";
            var button = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("Выбрать", $"select_{item.Id}")
            );

            await _bot.SendMessage(chatId, text, parseMode: ParseMode.Markdown,
                replyMarkup: button, cancellationToken: ct);
        }
    }

#endregion

    private void LoadMenu()
    {
        try
        {
            var fullPath = Path.GetFullPath(_menuPath);
            Console.WriteLine($"[Menu] Loading from: {fullPath}");

            if (!File.Exists(_menuPath))
            {
                Console.WriteLine("[Menu] File not found.");
                _menu = [];
                return;
            }
            
            var json = File.ReadAllText(_menuPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            _menu = JsonSerializer.Deserialize<List<MenuCategory>>(json, options) ?? new();

            Console.WriteLine($"[Menu] Loaded categories: {_menu.Count}");
            foreach (var c in _menu)
                Console.WriteLine($"[Menu]  - {c.Category}: {c.Items.Count} items");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Menu] Error: {e}");
            _menu = [];
        }
    }

    private void SaveOrder(Order order)
    {
        try
        {
            List<Order> orders = [];
            if (File.Exists(OrdersPath))
            {
                var json = File.ReadAllText(OrdersPath);
                orders = JsonSerializer.Deserialize<List<Order>>(json) ?? [];
            }
            
            orders.Add(order);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            File.WriteAllText(OrdersPath, JsonSerializer.Serialize(orders, options), System.Text.Encoding.UTF8);
            Console.WriteLine($"[Order] Saved: {order.ItemName} ({order.Price}₽) from {order.UserName}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Order] Error saving: {e.Message}");
        }
    }

    private static ReplyKeyboardMarkup MainMenuKeyboard() =>
        new([
            [new KeyboardButton("📋 Меню")],
            [new KeyboardButton("ℹ️ О нас"), new KeyboardButton("💬 Отзывы")]
        ]) { ResizeKeyboard = true };

#region Handlers

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message!.Text != null)
        {
            await HandleMessage(update.Message, ct);
            return;
        }

        if (update.Type == UpdateType.CallbackQuery) 
            await HandleCallback(update.CallbackQuery!, ct);
    }

    private async Task HandleMessage(Message message, CancellationToken ct)
    {
        string msg = message.Text!;
        long chatId = message.Chat.Id;

        if (msg == "/start")
        {
            await _bot.SendMessage(chatId,
                "👋 Добро пожаловать в Grillpoint!\nГорячие сэндвичи, приготовленные с душой.",
                replyMarkup: MainMenuKeyboard(),
                cancellationToken: ct);
            return;
        }

        if (msg == "📋 Меню")
        {
            await ShowCategories(chatId, ct);
            return;
        }

        var category = _menu.FirstOrDefault(c => c.Category == msg);
        if (category != null)
        {
            await ShowItems(chatId, category, ct);
            return;
        }
            
        await _bot.SendMessage(chatId, 
            "Пожалуйста, выберите из меню 👇",
            replyMarkup: MainMenuKeyboard(),
            cancellationToken: ct);

        if (_pendingOrders.TryGetValue(message.Chat.Id, out var order))
        {
            if (string.IsNullOrEmpty(order.DeliveryType))
            {
                if (msg.Contains("самовывоз", StringComparison.OrdinalIgnoreCase))
                {
                    order.DeliveryType = "Самовывоз";

                    await _bot.SendMessage(message.Chat.Id,
                        "Отлично! Укажите, пожалуйста, ваш номер телефона 📞",
                        cancellationToken: ct);
                    return;
                }
                else if (msg.Contains("достав", StringComparison.OrdinalIgnoreCase))
                {
                    order.DeliveryType = "Доставка";

                    await _bot.SendMessage(message.Chat.Id,
                        "Пожалуйста, введите адрес доставки 🏠",
                        cancellationToken: ct);
                    return;
                }
                else
                {
                    await _bot.SendMessage(message.Chat.Id,
                        "Выберите вариант: 🛍 Самовывоз или 🚚 Доставка",
                        cancellationToken: ct);
                    return;
                }
            }
            
            switch (order.DeliveryType)
            {
                case "Доставка" when string.IsNullOrEmpty(order.Address):
                    order.Address = msg;
                    await _bot.SendMessage(message.Chat.Id,
                        "Введите удобное время доставки (например, 19:30) ⏰",
                        cancellationToken: ct);
                    return;
                case "Доставка" when string.IsNullOrEmpty(order.DeliveryTime):
                    order.DeliveryTime = msg;
                    await _bot.SendMessage(message.Chat.Id,
                        "Укажите номер телефона 📞",
                        cancellationToken: ct);
                    return;
            }
            
            if (string.IsNullOrEmpty(order.ContactPhone))
            {
                order.ContactPhone = msg;

                SaveOrder(order);
                _pendingOrders.Remove(message.Chat.Id);

                await _bot.SendMessage(message.Chat.Id,
                    "✅ Спасибо! Ваш заказ принят и передан на обработку 🙌",
                    replyMarkup: MainMenuKeyboard(),
                    cancellationToken: ct);

                string notify = 
                    $"🆕 *Новый заказ*\n" +
                    $"🍔 {order.ItemName} — {order.Price} ₽\n" +
                    $"🚚 {order.DeliveryType}\n" +
                    (string.IsNullOrWhiteSpace(order.Address) ? "" : $"🏠 {order.Address}\n") +
                    (string.IsNullOrWhiteSpace(order.DeliveryTime) ? "" : $"⏰ {order.DeliveryTime}\n") +
                    $"📞 {order.ContactPhone}\n" +
                    $"👤 {order.UserName} (`{order.UserId}`)\n" +
                    $"🕒 {DateTime.Now:HH:mm}";

                await _bot.SendMessage(_adminChatId, notify, parseMode: ParseMode.Markdown, cancellationToken: ct);
                return;
            }
        }
    }

    private async Task HandleCallback(CallbackQuery query, CancellationToken ct)
    {
        var chatId = query.Message!.Chat.Id;
        var data = query.Data ?? "";

        if (data.StartsWith("select_"))
        {
            var id = data.Replace("select_", "");
            var item = _menu.SelectMany(c => c.Items).FirstOrDefault(i => i.Id == id);
        
            if (item == null)
            {
                await _bot.AnswerCallbackQuery(query.Id, "❌ Позиция не найдена", cancellationToken: ct);
                return;
            }
        
            string text = $"🍔 *{item.Name}*\n💰 {item.Price} ₽\n\nПодтвердить заказ?";
            var buttons = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"confirm_{item.Id}"),
                    InlineKeyboardButton.WithCallbackData("🔁 Изменить", "menu_back")
                }
            });
        
            await _bot.EditMessageText(chatId, query.Message.MessageId, text,
                parseMode: ParseMode.Markdown, replyMarkup: buttons, cancellationToken: ct);
        }
        else if (data.StartsWith("confirm_"))
        {
            var id = data.Replace("confirm_", "");
            var item = _menu.SelectMany(c => c.Items).FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            var order = new Order
            {
                ItemId = item.Id,
                ItemName = item.Name,
                Price = item.Price,
                UserId = query.From.Id,
                UserName = string.Join(' ',
                        new[] { query.From.FirstName, query.From.LastName }
                            .Where(s => !string.IsNullOrWhiteSpace(s)))
                            .Trim()
            };

            _pendingOrders[query.From.Id] = order;

            var markup = new ReplyKeyboardMarkup(
            [
                [
                    new KeyboardButton("🛍 Самовывоз"), 
                    new KeyboardButton("🚚 Доставка")
                ]
            ]) { ResizeKeyboard = true };

            await _bot.SendMessage(query.From.Id,
                "Выберите способ получения заказа:", replyMarkup: markup, cancellationToken: ct);

            await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        }
        else if (data == "menu_back")
        {
            await ShowCategories(chatId, ct);
            await _bot.AnswerCallbackQuery(query.Id, cancellationToken: ct);
        }
    }

    public static Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, HandleErrorSource source, CancellationToken ct)
    {
        Console.WriteLine($"Ошибка ({source}): {ex.Message}");
        return Task.CompletedTask;
    }

#endregion
}
