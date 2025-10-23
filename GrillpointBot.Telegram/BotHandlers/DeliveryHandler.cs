using System.Collections.Concurrent;
using System.Xml.Linq;
using GrillpointBot.Core.Common;
using GrillpointBot.Core.Config;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GrillpointBot.Telegram.BotHandlers;

public class DeliveryHandler(ITelegramBotClient bot, IOrderService orders, AppSettings config)
{
    // временное хранилище незавершённых заказов
    private readonly ConcurrentDictionary<long, Order> _pending = new();

    private string _pickup => Constants.Pickup.ToLower();
    private string _delivery => Constants.Delivery.ToLower();
    
    /// <summary>
    /// Запуск сценария доставки после подтверждения товара.
    /// </summary>
    public async Task StartDeliveryFlowAsync(Order order, CancellationToken ct)
    {
        _pending[order.UserId] = order;

        var markup = new ReplyKeyboardMarkup(
            [
                [ new KeyboardButton("🛍 Самовывоз"), new KeyboardButton("🚚 Доставка") ]
            ])
            { ResizeKeyboard = true };

        await bot.SendMessage(order.UserId,
            "Выберите способ получения заказа:", replyMarkup: markup, cancellationToken: ct);
    }
    
    /// <summary>
    /// Обработка текстовых сообщений пользователя во время оформления доставки.
    /// </summary>
    public async Task<bool> HandleDeliveryMessageAsync(Message message, CancellationToken ct)
    {
        if (!_pending.TryGetValue(message.Chat.Id, out var order))
            return false; // сообщение не в контексте доставки

        var msg = message.Text ?? "";

        // Шаг 1: выбор способа
        if (string.IsNullOrEmpty(order.DeliveryType))
        {
            if (msg.Contains(_pickup, StringComparison.OrdinalIgnoreCase))
            {
                order.DeliveryType = Constants.Pickup;
                await bot.SendMessage(message.Chat.Id,
                    "Отлично! Укажите, пожалуйста, ваш номер телефона 📞",
                    cancellationToken: ct);
                return true;
            }
            if (msg.Contains(_delivery, StringComparison.OrdinalIgnoreCase))
            {
                order.DeliveryType = Constants.Delivery;
                await bot.SendMessage(message.Chat.Id,
                    "Пожалуйста, введите адрес доставки 🏠",
                    cancellationToken: ct);
                return true;
            }

            await bot.SendMessage(message.Chat.Id,
                "Выберите вариант: 🛍 Самовывоз или 🚚 Доставка",
                cancellationToken: ct);
            return true;
        }

        // Шаг 2: если доставка — адрес и время
        if (order.DeliveryType == Constants.Delivery)
        {
            if (string.IsNullOrEmpty(order.Address))
            {
                order.Address = msg;
                await bot.SendMessage(message.Chat.Id,
                    "Введите удобное время доставки (например, 19:30) ⏰",
                    cancellationToken: ct);
                return true;
            }

            if (string.IsNullOrEmpty(order.DeliveryTime))
            {
                order.DeliveryTime = msg;
                await bot.SendMessage(message.Chat.Id,
                    "Укажите номер телефона 📞",
                    cancellationToken: ct);
                return true;
            }
        }

        // Шаг 3: номер телефона
        if (string.IsNullOrEmpty(order.ContactPhone))
        {
            order.ContactPhone = msg;

            // Завершение
            await FinalizeOrderAsync(order, ct);
            _pending.TryRemove(message.Chat.Id, out _);
            return true;
        }

        return false;
    }

    private async Task FinalizeOrderAsync(Order order, CancellationToken ct)
    {
        await orders.CreateOrderAsync(order);

        await bot.SendMessage(order.UserId,
            "✅ Спасибо! Ваш заказ принят и передан на обработку 🙌",
            cancellationToken: ct);

        string notify = TelegramNotifier.FormatAdminNotification(order);
        await bot.SendMessage(config.AdminChatId, notify, parseMode: ParseMode.Markdown, cancellationToken: ct);
    }
}