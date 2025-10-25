using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface IOrderService
{
    Task<Order> CreateAsync(Order order);
    Task<Order?> GetAsync(string id);
    Task<IEnumerable<Order>> GetAllOrdersAsync();
    Task<IEnumerable<Order>> GetByUserAsync(long userId);
    Task UpdateStatusAsync(string id, OrderStatus status);
}