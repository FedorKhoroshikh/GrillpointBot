using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface IOrderRepository
{
    Task SaveOrderAsync(Order order);
    Task<List<Order>> LoadOrdersAsync();
}