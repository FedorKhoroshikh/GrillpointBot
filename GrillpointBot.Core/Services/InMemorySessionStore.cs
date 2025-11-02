using System.Collections.Concurrent;
using GrillpointBot.Core.Interfaces;
using GrillpointBot.Core.Models;

namespace GrillpointBot.Core.Services;

public class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<long, Session> _sessions = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(6);
    private readonly Timer _gc;
    
    public InMemorySessionStore()
    {
        _gc = new Timer(_ => CollectGarbage(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
    }
    
    public Task<Session> GetOrCreateAsync(long userId)
    {
        if (_sessions.TryGetValue(userId, out var s))
        {
            if ((DateTime.UtcNow - s.LastUpdatedUtc).TotalHours > 4)
            {
                _sessions.TryRemove(userId, out _);
                s = new Session { UserId = userId }; 
            }
            return Task.FromResult(s);
        }

        var newSession = new Session { UserId = userId };
        _sessions[userId] = newSession;
        return Task.FromResult(newSession);
    }
    
    public Task UpsertAsync(Session session)
    {
        session.LastUpdatedUtc = DateTime.UtcNow;
        _sessions[session.UserId] = session;
        return Task.CompletedTask;
    }
    
    public Task<Session?> GetAsync(long userId)
    {
        _sessions.TryGetValue(userId, out var s);
        return Task.FromResult(s);
    }

    public Task RemoveAsync(long userId)
    {
        _sessions.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    private void CollectGarbage()
    {
        var border = DateTime.UtcNow - _ttl;
        foreach (var kv in _sessions)
        {
            if (kv.Value.LastUpdatedUtc < border)
                _sessions.TryRemove(kv.Key, out _);
        }
    }
}