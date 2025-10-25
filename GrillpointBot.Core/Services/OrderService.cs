using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Services;

public class OrderService(IOrderRepository orderRepository) : IOrderService
{
    public async Task<Order> CreateAsync(Order order)
    {
        await orderRepository.SaveOrderAsync(order);
        return order;
    }

    public Task<Order?> GetAsync(string id)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        => await orderRepository.LoadOrdersAsync(); 

    public async Task<IEnumerable<Order>> GetByUserAsync(long userId)
    {
        var allOrders = await orderRepository.LoadOrdersAsync();
        return allOrders.Where(o => o.UserId == userId);
    }

    public Task UpdateStatusAsync(string id, OrderStatus status)
    {
        throw new NotImplementedException();
    }
}