using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Services;

public class OrderService(IOrderRepository orderRepository) : IOrderService
{
    public async Task<Order> CreateOrderAsync(Order order)
    {
        await orderRepository.SaveOrderAsync(order);
        return order;
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync()
        => await orderRepository.LoadOrdersAsync(); 

    public async Task<IEnumerable<Order>> GetOrdersByUserAsync(long userId)
    {
        var allOrders = await orderRepository.LoadOrdersAsync();
        return allOrders.Where(o => o.UserId == userId);
    }
}