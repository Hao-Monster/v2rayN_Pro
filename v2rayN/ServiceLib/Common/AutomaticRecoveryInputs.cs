namespace ServiceLib.Common;

// Speed tests refresh and sort the view. Compare the underlying inputs across slow probes.
internal sealed record AutomaticRecoveryInputs(
    HashSet<(string Id, string Url, string MoreUrl)> Subscriptions, HashSet<string> Profiles)
{
    private sealed class ProfileKey
    {
        public string IndexId { get; set; } = string.Empty;
    }

    internal static async Task<AutomaticRecoveryInputs> ReadAsync()
    {
        var subscriptions = await AppManager.Instance.SubItems() ?? [];
        var profiles = await SQLiteHelper.Instance.QueryAsync<ProfileKey>("SELECT IndexId FROM ProfileItem");
        return new(subscriptions.Where(s => s.Enabled && SubscriptionHandler.IsValidSubscription(s, string.Empty))
            .Select(s => (s.Id, s.Url, s.MoreUrl)).ToHashSet(), profiles.Select(p => p.IndexId).ToHashSet(StringComparer.Ordinal));
    }

    internal bool Matches(AutomaticRecoveryInputs other) =>
        Subscriptions.Count > 0 && Profiles.Count > 0
        && Subscriptions.SetEquals(other.Subscriptions) && Profiles.SetEquals(other.Profiles);
}
