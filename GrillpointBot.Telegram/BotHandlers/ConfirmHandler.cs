using GrillpointBot.Core.Common;
using GrillpointBot.Core.Config;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Telegram.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace GrillpointBot.Telegram.BotHandlers;

public class ConfirmHandler(
    ITelegramBotClient bot,
    IMenuService menu,
    ISessionStore sessions,
    IOrderService orders,
    AppSettings config)
{
    public async Task HandleConfirm(CallbackQuery cq, CancellationToken ct)
    {
        var s = await sessions.GetOrCreateAsync(cq.From.Id);
        if (s.DraftQty.Count == 0)
        {
            await bot.AnswerCallbackQuery(cq.Id, "Корзина пуста", cancellationToken: ct);
            return;
        }
        
        // Собираем заказ
        var order = new Order
        {
            UserId = cq.From.Id,
            UserName = string.Join(' ', new[] { cq.From.FirstName, cq.From.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
            Delivery = new DeliveryInfo
            {
                Method      = s.DraftDelivery.Method,
                
                FullAddress =  s.DraftDelivery.FullAddress,
                
                City     = s.DraftDelivery.City,
                Locality = s.DraftDelivery.Locality,
                Street   = s.DraftDelivery.Street,
                House    = s.DraftDelivery.House,
                POI      = s.DraftDelivery.POI,
                Postcode = s.DraftDelivery.Postcode,
                
                Lat = s.DraftDelivery.Lat,
                Lon = s.DraftDelivery.Lon,
                
                ScheduledTime = s.DraftDelivery.ScheduledTime,
                PhoneDisplay  = s.DraftDelivery.Phone
            },
            Comment = s.Comment
        };
        
        foreach (var (id, qty) in s.DraftQty)
        {
            var it = await menu.GetItemByIdAsync(id);
            if (it is null) continue;

            order.Lines.Add(new OrderLine
            {
                ItemId = it.Id,
                ItemName = it.Name,
                UnitPrice = it.Price,
                WeightG = it.Weight,
                Quantity = qty
            });
        }
        
        await orders.CreateAsync(order); // сохраняем JSON (MVP):contentReference[oaicite:5]{index=5}
        
        // Пользовательская карточка «спасибо»
        await bot.EditMessageText(
            cq.Message!.Chat.Id, cq.Message.MessageId,
            $"✅ Спасибо за заказ!\nНомер: <b>#{order.Id[..6]}</b>\nИтого: <b>{order.Total:0.#} ₽</b>\n" +
            $"{(order.Delivery.Method == DeliveryMethod.Delivery ? "Способ: доставка" : "Способ: самовывоз")}",
            parseMode: ParseMode.Html, replyMarkup: Kb.BackToWelcome, cancellationToken: ct);

        await bot.AnswerCallbackQuery(cq.Id, cancellationToken: ct);
        
        // Уведомление админу (используем AdminChatIdNum из конфигурации):contentReference[oaicite:6]{index=6}
        if (config.AdminChatIdNum > 0)
        {
            var lines = new List<string>
            {
                $"🆕 Заказ #{order.Id[..6]} от {order.UserName} (id {order.UserId})",
                $"Способ: {(order.Delivery.Method == DeliveryMethod.Delivery ? Constants.Delivery : Constants.Pickup)}",
            };
            if (order.Delivery.Method == DeliveryMethod.Delivery)
                lines.Add($"Адрес: {order.Delivery.FullAddress}");
            if (!string.IsNullOrWhiteSpace(order.Delivery.PhoneDisplay))
                lines.Add($"Телефон: {order.Delivery.PhoneDisplay}");
            if (!string.IsNullOrWhiteSpace(order.Comment))
                lines.Add($"Комментарий: {order.Comment}");
            
            lines.Add("———");
            lines.AddRange(order.Lines
                .Select(l => $"{l.ItemName} × {l.Quantity} = {l.LineTotal:0.#} ₽"));

            lines.Add($"ИТОГО: <b>{order.Total:0.#} ₽</b>");

            await bot.SendMessage(config.AdminChatIdNum, string.Join("\n", lines),
                parseMode: ParseMode.Html, cancellationToken: ct);
            
            // Обнуляем рабочую часть сессии (после успешного заказа)
            s.Cart.Clear();
            s.DraftQty.Clear();
            s.Comment = s.DraftComment = null;
            s.CartMessageId = null;
            s.CommentMessageIds.Clear();
            s.State = FlowState.Idle;
            await sessions.UpsertAsync(s);
        }
    }
}