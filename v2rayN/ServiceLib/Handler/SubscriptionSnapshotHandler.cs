namespace ServiceLib.Handler;

internal sealed class SubscriptionSnapshot
{
    public string SubId { get; set; } = string.Empty;
    public string? SourceHash { get; set; }
    public string? ActiveIndexId { get; set; }
    public long CreatedAt { get; set; }
    public List<ProfileItem> Profiles { get; set; } = [];
    public List<ProfileExItem> ProfileExs { get; set; } = [];
}

public static class SubscriptionSnapshotHandler
{
    public static async Task<bool> CaptureAsync(Config config, string subId)
    {
        if (subId.IsNullOrEmpty())
        {
            return false;
        }

        var subscription = await AppManager.Instance.GetSubItem(subId);
        if (subscription is null)
        {
            return false;
        }

        var profiles = (await AppManager.Instance.ProfileItems(subId))?
            .Where(item => item.IsSub)
            .ToList() ?? [];
        if (profiles.Count == 0)
        {
            return false;
        }

        var ids = profiles.Select(item => item.IndexId).ToHashSet(StringComparer.Ordinal);
        var profileExs = (await ProfileExManager.Instance.GetProfileExs())
            .Where(item => ids.Contains(item.IndexId))
            .Select(CloneProfileEx)
            .ToList();
        var activeIndexId = ids.Contains(config.IndexId) ? config.IndexId : null;

        // A snapshot is only promoted to "last known good" after at least one
        // successful real-ping result, or while it contains the active node.
        if (activeIndexId.IsNullOrEmpty() && profileExs.All(item => item.Delay <= 0))
        {
            return false;
        }

        var snapshot = new SubscriptionSnapshot
        {
            SubId = subId,
            SourceHash = SourceHash(subscription),
            ActiveIndexId = activeIndexId,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Profiles = profiles,
            ProfileExs = profileExs
        };
        var path = GetSnapshotPath(subId);
        var tempPath = $"{path}.tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(tempPath, JsonUtils.Serialize(snapshot));
        File.Move(tempPath, path, true);
        Logging.SaveLog($"Saved last-known-good subscription snapshot. Subscription={subId}, Nodes={profiles.Count}.");
        return true;
    }

    public static async Task<bool> RestoreAsync(Config config, string subId)
    {
        var path = GetSnapshotPath(subId);
        var subscription = await AppManager.Instance.GetSubItem(subId);
        if (!File.Exists(path) || subscription is null)
        {
            return false;
        }

        try
        {
            var snapshot = JsonUtils.Deserialize<SubscriptionSnapshot>(await File.ReadAllTextAsync(path));
            if (snapshot is null || snapshot.SubId != subId || snapshot.SourceHash != SourceHash(subscription) || snapshot.Profiles.Count == 0)
            {
                return false;
            }

            await ConfigHandler.RemoveServersViaSubid(config, subId, true);
            await SQLiteHelper.Instance.InsertAllAsync(snapshot.Profiles);
            foreach (var item in snapshot.ProfileExs)
            {
                ProfileExManager.Instance.SetTestDelay(item.IndexId, item.Delay);
                ProfileExManager.Instance.SetTestSpeed(item.IndexId, item.Speed);
                ProfileExManager.Instance.SetSort(item.IndexId, item.Sort);
                ProfileExManager.Instance.SetTestMessage(item.IndexId, item.Message ?? string.Empty);
                ProfileExManager.Instance.SetTestIpInfo(item.IndexId, item.IpInfo ?? string.Empty);
            }
            await ProfileExManager.Instance.SaveTo();

            if (snapshot.ActiveIndexId.IsNotEmpty()
                && snapshot.Profiles.Any(item => item.IndexId == snapshot.ActiveIndexId))
            {
                await ConfigHandler.SetDefaultServerIndex(config, snapshot.ActiveIndexId);
            }

            Logging.SaveLog($"Restored last-known-good subscription snapshot. Subscription={subId}, Nodes={snapshot.Profiles.Count}.");
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"Restore subscription snapshot failed. Subscription={subId}.", ex);
            return false;
        }
    }

    public static DateTimeOffset? TryGetSnapshotTimestamp(string? subId)
    {
        if (subId.IsNullOrEmpty())
        {
            return null;
        }

        try
        {
            var path = GetSnapshotPath(subId);
            if (!File.Exists(path))
            {
                return null;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(path);
            if (lastWriteUtc.Year < 2020)
            {
                return null;
            }

            return new DateTimeOffset(DateTime.SpecifyKind(lastWriteUtc, DateTimeKind.Utc)).ToLocalTime();
        }
        catch
        {
            return null;
        }
    }

    internal static void Invalidate(string subId)
    {
        try
        {
            File.Delete(GetSnapshotPath(subId));
        }
        catch (DirectoryNotFoundException)
        {
            // No snapshot has been saved yet.
        }
    }

    private static string SourceHash(SubItem sub) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { sub.Url, sub.MoreUrl })));

    internal static string GetSnapshotFileName(string subId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(subId));
        return $"{Convert.ToHexString(hash).ToLowerInvariant()}.json";
    }

    private static string GetSnapshotPath(string subId)
    {
        return Path.Combine(Utils.GetConfigPath("subscriptionSnapshots"), GetSnapshotFileName(subId));
    }

    private static ProfileExItem CloneProfileEx(ProfileExItem item)
    {
        return new ProfileExItem
        {
            IndexId = item.IndexId,
            Delay = item.Delay,
            Speed = item.Speed,
            Sort = item.Sort,
            Message = item.Message,
            IpInfo = item.IpInfo
        };
    }
}
