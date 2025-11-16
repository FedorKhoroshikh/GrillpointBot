using System.Text.RegularExpressions;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using Constants = GrillpointBot.Core.Common.Constants;

namespace GrillpointBot.Telegram.Utilities;

public class MessageFormatter
{
    public static async Task<string> BuildOrderSummaryAsync(Session s, IMenuService menu)
    {
        decimal total = 0;
        int counter = 0;
        var lines = new List<string> { "<b>Ваш заказ:</b>\n" };
        foreach (var (id, qty) in s.DraftQty)
        {
            counter++;
            var it = await menu.GetItemByIdAsync(id);
            if (it is null) continue;
            var sum = it.Price * qty;
            total += sum;

            lines.Add($"({counter})  <b>{it.Category}</b> — {it.Name}\n" +
                      $"     <i>{it.Weight} г</i> · {it.Price:0.#} ₽ × {qty} = <b>{sum:0.#} ₽</b>\n");
        }
        
        lines.Add($"<b>Итого:</b> {total:0.#} ₽");
        
        if (!string.IsNullOrWhiteSpace(s.Comment)) 
            lines.Add($"\n<i>Комментарий к заказу:</i> {s.Comment}");
        
        return string.Join("\n", lines);
    }
    
    public static async Task<string> BuildDeliverySummaryAsync(Session s)
    {
        var lines = new List<string>
        {
            "____________________\n",
            "<b>Данные получения заказа:</b>\n",
            $"<i>> Способ:</i> {(s.DraftDelivery.Method == DeliveryMethod.Delivery ? Constants.Delivery : Constants.Pickup)}"
        };

        if (!string.IsNullOrWhiteSpace(s.DraftDelivery.AddressDisplay)) 
            lines.Add($"<i>> Адрес:</i> {s.DraftDelivery.AddressDisplay}");
        if (!string.IsNullOrWhiteSpace(s.DraftDelivery.PhoneDisplay)) 
            lines.Add($"<i>> Телефон:</i> {s.DraftDelivery.PhoneDisplay}");
        if (!string.IsNullOrWhiteSpace(s.UserNick)) 
            lines.Add($"<i>> Никнейм:</i> {s.UserNick}");
        if (s.DraftDelivery.ScheduledTime.HasValue) 
            lines.Add($"<i>> Время:</i> {s.DraftDelivery.ScheduledTime:dd.MM.yyyy HH:mm}");

        return string.Join("\n", lines);
    }
}