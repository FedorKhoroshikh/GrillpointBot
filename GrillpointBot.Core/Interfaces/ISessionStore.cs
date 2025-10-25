using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface ISessionStore
{
    Task<Session?> GetAsync(long userId);
    Task SetAsync(Session session, TimeSpan ttl);
    Task RemoveAsync(long userId);
}