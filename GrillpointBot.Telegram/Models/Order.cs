namespace GrillpointBot.Telegram.Models;

public class Order
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ItemId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public decimal Price { get; set; }
    
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    
    public string DeliveryType { get; set; } = "";
    public string Address { get; set; } = "";
    public string ContactPhone { get; set; } = "";
    public string DeliveryTime { get; set; } = "";
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
