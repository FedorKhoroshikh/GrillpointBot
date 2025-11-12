using System.Text.RegularExpressions;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using Constants = GrillpointBot.Core.Common.Constants;

namespace GrillpointBot.Telegram.Utilities;

public class MessageFormatter
{
    public static async Task<string> BuildOrderSummaryAsync(Session session, IMenuService menu)
    {
        decimal total = 0;
        int counter = 0;
        var lines = new List<string> { "<b>Ваш заказ:</b>\n" };
        foreach (var (id, qty) in session.DraftQty)
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
        return string.Join("\n", lines);
    }
    
    public static async Task<string> BuildDeliverySummaryAsync(Session s)
    {
        var lines = new List<string>
        {
            "____________________\n",
            "<b>Данные получения заказа:</b>\n",
            $"<i>Способ:</i> {(s.DraftDelivery.Method == DeliveryMethod.Delivery ? Constants.Delivery : Constants.Pickup)}"
        };

        if (s.DraftDelivery.Method == DeliveryMethod.Delivery)
            lines.Add($"<i>> Адрес:</i> {s.DraftDelivery.FullAddress}");
        if (!string.IsNullOrWhiteSpace(s.DraftDelivery.Phone))
            lines.Add($"<i>> Телефон:</i> {FormatPhone(s.DraftDelivery.Phone)}");
        if (!string.IsNullOrWhiteSpace(s.UserNick))
            lines.Add($"<i>> Никнейм:</i> {s.UserNick}");
        if (s.DraftDelivery.ScheduledTime.HasValue)
            lines.Add($"<i>> Время:</i> {s.DraftDelivery.ScheduledTime:dd.MM HH:mm}");

        return string.Join("\n", lines);
    }
    
    private static string FormatPhone(string raw)
    {
        var digits = Regex.Replace(raw, @"\D", "");
        if (digits.StartsWith("8")) digits = "7" + digits[1..];
        if (!digits.StartsWith("7")) digits = "7" + digits;

        return digits.Length == 11
            ? $"+7 ({digits[1..4]}) {digits[4..7]}-{digits[7..9]}-{digits[9..11]}"
            : "+" + digits;
    }
}