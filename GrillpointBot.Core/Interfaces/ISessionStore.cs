using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Interfaces;

public interface ISessionStore
{
    Task<Session?> GetAsync(long userId);
    Task UpsertAsync(Session session);
    Task<Session> GetOrCreateAsync(long userId);
    Task RemoveAsync(long userId);
}