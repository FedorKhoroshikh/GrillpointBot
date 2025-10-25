namespace GrillpointBot.Core.Models;

public sealed class Review
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = "";
    public long UserId { get; set; }
    public int Rate { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}