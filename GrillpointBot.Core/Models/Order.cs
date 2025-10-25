namespace GrillpointBot.Core.Models;

public enum OrderStatus
{
    Created,        // Создан.
    Confirmed,      // Подтвержден. 
    Cooking,        // Готовится... 
    Ready,          // Готов.
    OnTheWay,       // В пути...
    Delivered,      // Доставлен.
    Cancelled       // Отменен.
}

public sealed class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public List<OrderLine> Lines { get; set; } = [];
    public DeliveryInfo Delivery { get; set; } = new();
    public string? Comment { get; set; }
    public decimal Total => Lines.Sum(l => l.LineTotal);
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
