namespace GrillpointBot.Core.Models;

public enum DeliveryMethod
{
    Pickup,     // Самовывоз
    Delivery    // Доставка
}

public sealed class DeliveryInfo {
    public DeliveryMethod Method { get; set; }
    public string? AddressText { get; set; }
    public (double lat, double lon)? Geo { get; set; }   // Геолокация (v2)
    public string? TimeText { get; set; }                // «19:30»
    public string? ContactPhone { get; set; }
}