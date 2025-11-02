using System.Text.Encodings.Web;
using System.Text.Json;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;
using GrillpointBot.Core.Common;

namespace GrillpointBot.Infrastructure.Repositories;

public class JsonOrderRepository : IOrderRepository
{
    private string OrdersPath => Constants.Orders;
    
    public async Task SaveOrderAsync(Order order)
    {
        try
        {
            List<Order> orders = [];
            if (File.Exists(OrdersPath))
            {
                var json = await File.ReadAllTextAsync(OrdersPath);
                orders = JsonSerializer.Deserialize<List<Order>>(json) ?? [];
            }
            
            orders.Add(order);

            var options = new JsonSerializerOptions {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            await File.WriteAllTextAsync(OrdersPath, JsonSerializer.Serialize(orders, options), System.Text.Encoding.UTF8);
            Console.WriteLine($"[Order] Saved: {order.Id} ({order.Total}₽) from {order.UserName}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Order] Error saving: {e.Message}");
        }
    }

    public async Task<List<Order>> LoadOrdersAsync()
    {
        if (!File.Exists(OrdersPath)) return [];
        var json = await File.ReadAllTextAsync(OrdersPath);
        return JsonSerializer.Deserialize<List<Order>>(json) ?? [];
    }
}