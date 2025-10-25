namespace GrillpointBot.Core.Models;

public sealed class OrderLine {
    public string ItemId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public decimal? WeightG { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}