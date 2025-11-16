using System.Globalization;
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
            .AppendLine(string.IsNullOrWhiteSpace(order.Delivery.FullAddress) ? "" : $"🏠 {order.Delivery.FullAddress}")
            .AppendLine(string.IsNullOrWhiteSpace(order.Delivery.ScheduledTime.ToString()) 
                ? "" 
                : $"⏰ {order.Delivery.ScheduledTime.ToString()}")
            .AppendLine($"📞 {order.Delivery.PhoneDisplay}")
            .AppendLine($"👤 {order.UserName} (`{order.UserId}`)")
            .AppendLine($"🕒 {DateTime.Now:HH:mm}");
        return sb.ToString();
    }
}