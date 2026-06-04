namespace SignInWithGoogle.Services
{
    // Registered as singleton — one instance for the lifetime of the app
    public class ConnectionTracker
    {
        // userId → set of connectionIds (one user can have multiple tabs/devices)
        private readonly Dictionary<Guid, HashSet<string>> _connections = new();
        private readonly Lock _lock = new();

        public void Add(Guid userId, string connectionId)
        {
            lock (_lock)
            {
                if (!_connections.TryGetValue(userId, out var connections))
                {
                    connections = new HashSet<string>();
                    _connections[userId] = connections;
                }
                connections.Add(connectionId);
            }
        }

        public void Remove(Guid userId, string connectionId)
        {
            lock (_lock)
            {
                if (_connections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connectionId);
                    if (connections.Count == 0)
                        _connections.Remove(userId);
                }
            }
        }

        public IReadOnlySet<string> GetConnections(Guid userId)
        {
            lock (_lock)
            {
                return _connections.TryGetValue(userId, out var connections)
                    ? connections
                    : new HashSet<string>();
            }
        }

        public bool IsOnline(Guid userId)
        {
            lock (_lock)
            {
                return _connections.ContainsKey(userId) &&
                       _connections[userId].Count > 0;
            }
        }
    }
}
