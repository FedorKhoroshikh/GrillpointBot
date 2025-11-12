namespace GrillpointBot.Core.Models;

public enum DeliveryMethod
{
    Pickup,     // Самовывоз
    Delivery    // Доставка
}

public sealed class DeliveryInfo
{
    public DeliveryMethod Method { get; set; } = DeliveryMethod.Pickup;
    
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Street { get; set; }
    public string? House { get; set; }
    public string FullAddress =>
        string.Join(", ", new[] { City, Street, House }.Where(s => !string.IsNullOrWhiteSpace(s)));
    
    public (double lat, double lon)? Geo { get; set; }   // Геолокация (v2)
    public string? Phone { get; set; } = "";
    public DateTime? ScheduledTime { get; set; }
}