namespace GrillpointBot.Core.Models;

public enum DeliveryMethod
{
    Pickup,     // Самовывоз
    Delivery    // Доставка
}

public sealed class DeliveryInfo
{
    public DeliveryMethod Method { get; set; } = DeliveryMethod.Pickup;
    
    public string? FullAddress { get; set; }
    public string? AddressDisplay { get; set; }
    public string? City { get; set; }
    public string? Locality { get; set; } 
    public string? Suburb { get; set; } 
    public string? Street { get; set; }
    public string? House { get; set; }
    public string? POI { get; set; }  
    public string? Postcode { get; set; }
    
    public double Lat { get; set; }
    public double Lon { get; set; }
    
    public DateTime? ScheduledTime { get; set; }
    
    public string? PhoneDisplay { get; set; } = "";
    public string? Phone { get; set; } = "";
}