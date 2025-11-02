using System.Text;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Telegram.Services;

public class MessageFormatter
{
    public static string FormatAdminNotification(Order order)
    {
        var sb = new StringBuilder()
            .AppendLine($"🆕 Новый заказ #{order.Id}")
            .AppendLine($"🍔 {order.Lines} — {order.Total} ₽")
            .AppendLine($"🚚 {order.Delivery.Method}")
            .AppendLine(string.IsNullOrWhiteSpace(order.Delivery.AddressText) ? "" : $"🏠 {order.Delivery.AddressText}")
            .AppendLine(string.IsNullOrWhiteSpace(order.Delivery.TimeText) ? "" : $"⏰ {order.Delivery.TimeText}")
            .AppendLine($"📞 {order.Delivery.ContactPhone}")
            .AppendLine($"👤 {order.UserName} (`{order.UserId}`)")
            .AppendLine($"🕒 {DateTime.Now:HH:mm}");
        return sb.ToString();
    }
}