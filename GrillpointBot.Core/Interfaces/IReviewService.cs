using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface IReviewService
{
    Task<string> CreateAsync(Review order);
    Task<Review?>  GetAsync(string id);
    Task<IEnumerable<Review>> GetByUserAsync(long userId);
    Task<IEnumerable<Review>> GetAllAsync();
}