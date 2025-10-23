using System.Text;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Telegram.Services;

public class TelegramNotifier
{
    public static string FormatAdminNotification(Order order)
    {
        var sb = new StringBuilder()
            .AppendLine("🆕 *Новый заказ*")
            .AppendLine($"🍔 {order.ItemName} — {order.Price} ₽")
            .AppendLine($"🚚 {order.DeliveryType}")
            .AppendLine("🆕 *Новый заказ*")
            .AppendLine($"🍔 {order.ItemName} — {order.Price} ₽")
            .AppendLine($"🚚 {order.DeliveryType}")
            .AppendLine(string.IsNullOrWhiteSpace(order.Address) ? "" : $"🏠 {order.Address}")
            .AppendLine(string.IsNullOrWhiteSpace(order.DeliveryTime) ? "" : $"⏰ {order.DeliveryTime}")
            .AppendLine($"📞 {order.ContactPhone}")
            .AppendLine($"👤 {order.UserName} (`{order.UserId}`)")
            .AppendLine($"🕒 {DateTime.Now:HH:mm}");
        return sb.ToString();
    }

}