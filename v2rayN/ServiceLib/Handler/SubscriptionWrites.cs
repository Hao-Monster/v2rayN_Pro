namespace ServiceLib.Handler;

// SQLite has one writer. Hold this gate only for local mutations, never for downloads or UI callbacks.
internal static class SubscriptionWrites
{
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly Dictionary<string, long> Versions = new(StringComparer.Ordinal);
    private static readonly AsyncLocal<WriteScope?> CurrentScope = new();

    private sealed class WriteScope
    {
        public volatile bool Active = true;
        public string? ImportingSubscription;
    }

    private static async Task<T> Locked<T>(Func<Task<T>> action)
    {
        if (CurrentScope.Value is { Active: true })
        {
            return await action();
        }

        await Gate.WaitAsync();
        var scope = new WriteScope();
        CurrentScope.Value = scope;
        try
        {
            return await action();
        }
        finally
        {
            scope.Active = false;
            CurrentScope.Value = null;
            Gate.Release();
        }
    }

    internal static Task<int> Edit(IEnumerable<string> ids, Func<Task<int>> write) => Locked(async () =>
    {
        foreach (var id in ids.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (CurrentScope.Value!.ImportingSubscription != id)
            {
                SubscriptionSnapshotHandler.Invalidate(id);
            }
            Versions[id] = Versions.GetValueOrDefault(id) + 1;
        }
        return await write();
    });

    internal sealed class Ticket(string id, long version)
    {
        private long _expected = version;
        private bool _restored;

        internal bool IsCurrent => Versions.GetValueOrDefault(id) == _expected;

        internal Task<bool> IfCurrent(Func<Task<bool>> read) => Locked(() =>
            IsCurrent ? read() : Task.FromResult(false));

        internal Task<bool> Apply(Func<Task<bool>> import) => Locked(async () =>
        {
            if (!IsCurrent)
            {
                return false;
            }
            Versions[id] = ++_expected;
            var scope = CurrentScope.Value!;
            scope.ImportingSubscription = id;
            try
            {
                return await import();
            }
            finally
            {
                scope.ImportingSubscription = null;
                // The importer can make nested profile writes for custom nodes.
                _expected = Versions[id];
            }
        });

        internal Task<bool> Restore(Func<Task<bool>> restore) => Locked(async () =>
        {
            if (_restored || !IsCurrent)
            {
                return false;
            }
            _restored = true;
            Versions[id] = ++_expected;
            return await restore();
        });
    }

    internal static Task<List<(SubItem Item, Ticket Ticket)>> ReadDownloadItems(string subId, Dictionary<string, Ticket>? tickets) =>
        Locked(async () =>
        {
            var items = await AppManager.Instance.SubItems() ?? [];
            var downloads = new List<(SubItem, Ticket)>();
            foreach (var item in items.Where(item => SubscriptionHandler.IsValidSubscription(item, subId)))
            {
                if (tickets is null)
                {
                    downloads.Add((item, new Ticket(item.Id, Versions.GetValueOrDefault(item.Id))));
                }
                else if (tickets.TryGetValue(item.Id, out var ticket) && ticket.IsCurrent)
                {
                    downloads.Add((item, ticket));
                }
            }
            return downloads;
        });

    internal static Task<List<string>> CurrentIds(Dictionary<string, Ticket> tickets) =>
        Locked(() => Task.FromResult(tickets.Where(pair => pair.Value.IsCurrent).Select(pair => pair.Key).ToList()));

    internal static Task<Dictionary<string, Ticket>> Capture(IEnumerable<string> ids) =>
        Locked(() => Task.FromResult(ids.Distinct(StringComparer.Ordinal)
            .ToDictionary(id => id, id => new Ticket(id, Versions.GetValueOrDefault(id)), StringComparer.Ordinal)));
}
