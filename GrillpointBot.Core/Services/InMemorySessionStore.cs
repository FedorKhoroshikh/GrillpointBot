using System.Collections.Concurrent;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Services;

public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<long, (Session session, DateTime expires)> _sessions = [];
    
    public Task<Session?> GetAsync(long userId)
    {
        if (_sessions.TryGetValue(userId, out var entry))
        {
            if (DateTime.UtcNow < entry.expires)
                return Task.FromResult<Session?>(entry.session);
            _sessions.TryRemove(userId, out _);
        }
        return Task.FromResult<Session?>(null);
    }

    public Task SetAsync(Session session, TimeSpan ttl)
    {
        _sessions[session.UserId] = (session, DateTime.UtcNow.Add(ttl));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(long userId)
    {
        _sessions.TryRemove(userId, out _);
        return Task.CompletedTask;
    }
}