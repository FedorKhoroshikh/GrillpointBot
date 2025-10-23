using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(Order order);
    Task<IEnumerable<Order>> GetAllOrdersAsync();
    Task<IEnumerable<Order>> GetOrdersByUserAsync(long userId);
}