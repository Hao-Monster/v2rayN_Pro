using System.Globalization;

namespace ServiceLib.Models.Dto;

public readonly record struct SubscriptionNodeMetrics(
    int TotalCount,
    int TestedCount,
    int AvailableCount,
    int FastCount,
    int MediumCount,
    int FailedCount,
    int? MedianDelayMs,
    DateTimeOffset? LastSpeedtestAt);

public readonly record struct SubscriptionAllSummary(
    int RemoteSubscriptionCount,
    int EnabledRemoteSubscriptionCount,
    long TotalUploadBytes,
    long TotalDownloadBytes);

public partial class SubscriptionCardItem : MyReactiveObject
{
    public const int FastDelayThresholdMs = 500;
    public static readonly TimeSpan SpeedtestStaleThreshold = TimeSpan.FromMinutes(30);

    private static readonly string[] _byteUnits = ["B", "KB", "MB", "GB", "TB", "PB"];

    public SubItem SubItem { get; private set; } = new();

    [Reactive]
    public partial string Id { get; set; } = string.Empty;

    [Reactive]
    public partial string Remarks { get; set; } = string.Empty;

    [Reactive]
    public partial string HeaderToolTip { get; set; } = string.Empty;

    [Reactive]
    public partial bool IsAllGroup { get; set; }

    [Reactive]
    public partial bool IsRemoteSubscription { get; set; }

    [Reactive]
    public partial bool IsLocalGroup { get; set; }

    [Reactive]
    public partial bool IsActiveConfig { get; set; }

    [Reactive]
    public partial bool IsDisabled { get; set; }

    [Reactive]
    public partial bool ShowUpdateButton { get; set; }

    [Reactive]
    public partial bool CanUpdateSubscription { get; set; }

    [Reactive]
    public partial string UpdateButtonText { get; set; } = "更新订阅";

    [Reactive]
    public partial string UpdateModeBadgeText { get; set; } = string.Empty;

    [Reactive]
    public partial string ProfileWebPageUrl { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasProfileWebPageUrl { get; set; }

    [Reactive]
    public partial string SupportUrl { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasSupportUrl { get; set; }

    [Reactive]
    public partial bool HasTrafficSection { get; set; }

    [Reactive]
    public partial bool HasQuotaLimit { get; set; }

    [Reactive]
    public partial bool ShowUnlimitedTrafficRow { get; set; }

    [Reactive]
    public partial string QuotaRemainingText { get; set; } = string.Empty;

    [Reactive]
    public partial string QuotaPercentText { get; set; } = string.Empty;

    [Reactive]
    public partial double QuotaPercentValue { get; set; }

    [Reactive]
    public partial double QuotaRemainingRatio { get; set; }

    [Reactive]
    public partial int QuotaFilledWeight { get; set; }

    [Reactive]
    public partial int QuotaEmptyWeight { get; set; } = 1000;

    [Reactive]
    public partial bool IsQuotaWarning { get; set; }

    [Reactive]
    public partial bool IsQuotaDanger { get; set; }

    [Reactive]
    public partial string UnlimitedTrafficText { get; set; } = string.Empty;

    [Reactive]
    public partial string TrafficDetailText { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasSubscriptionMeta { get; set; }

    [Reactive]
    public partial string ExpireText { get; set; } = string.Empty;

    [Reactive]
    public partial bool IsExpired { get; set; }

    [Reactive]
    public partial bool IsExpiringSoon { get; set; }

    [Reactive]
    public partial string SyncTimeText { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasNoteBox { get; set; }

    [Reactive]
    public partial string NoteBoxText { get; set; } = string.Empty;

    [Reactive]
    public partial string NoteBoxToolTip { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasExtraConfig { get; set; }

    [Reactive]
    public partial string ExtraConfigText { get; set; } = string.Empty;

    [Reactive]
    public partial bool HasNodeStats { get; set; }

    [Reactive]
    public partial bool ShowEmptyNodesHint { get; set; } = true;

    [Reactive]
    public partial string EmptyNodesHintText { get; set; } = "暂无节点";

    [Reactive]
    public partial string NodeCountsText { get; set; } = string.Empty;

    [Reactive]
    public partial string MedianDelayText { get; set; } = string.Empty;

    [Reactive]
    public partial string LastSpeedtestText { get; set; } = string.Empty;

    [Reactive]
    public partial string HealthBarToolTip { get; set; } = string.Empty;

    [Reactive]
    public partial int FastNodeCount { get; set; }

    [Reactive]
    public partial int MediumNodeCount { get; set; }

    [Reactive]
    public partial int FailedNodeCount { get; set; }

    [Reactive]
    public partial int UntestedSegmentWeight { get; set; } = 1;

    public void Apply(
        SubItem subItem,
        SubscriptionNodeMetrics metrics,
        bool isActiveConfig,
        SubscriptionAllSummary allSummary = default,
        DateTimeOffset? nowOverride = null)
    {
        ArgumentNullException.ThrowIfNull(subItem);

        var nowLocal = nowOverride ?? DateTimeOffset.Now;
        SubItem = subItem;
        Id = subItem.Id ?? string.Empty;
        Remarks = subItem.Remarks.IsNotEmpty() ? subItem.Remarks : "未命名分组";

        IsAllGroup = Id.IsNullOrEmpty();
        IsRemoteSubscription = !IsAllGroup && (subItem.Url.IsNotEmpty() || subItem.MoreUrl.IsNotEmpty());
        IsLocalGroup = !IsAllGroup && !IsRemoteSubscription;
        IsActiveConfig = isActiveConfig;
        IsDisabled = !IsAllGroup && !subItem.Enabled;

        if (IsAllGroup)
        {
            ShowUpdateButton = allSummary.RemoteSubscriptionCount > 0;
            CanUpdateSubscription = allSummary.EnabledRemoteSubscriptionCount > 0;
            UpdateButtonText = "更新全部";
            UpdateModeBadgeText = allSummary.RemoteSubscriptionCount > 0
                ? $"汇总 {allSummary.RemoteSubscriptionCount} 个订阅"
                : "全部配置";
            HeaderToolTip = $"{Remarks}（{UpdateModeBadgeText}）";
        }
        else if (IsRemoteSubscription)
        {
            ShowUpdateButton = true;
            CanUpdateSubscription = subItem.Enabled;
            UpdateButtonText = "更新订阅";
            UpdateModeBadgeText = FormatAutoUpdateBadge(subItem.AutoUpdateInterval);
            HeaderToolTip = IsDisabled ? $"{Remarks}（已禁用）" : Remarks;
        }
        else
        {
            ShowUpdateButton = false;
            CanUpdateSubscription = false;
            UpdateButtonText = "更新订阅";
            UpdateModeBadgeText = "本地分组";
            HeaderToolTip = $"{Remarks}（本地分组）";
        }

        ProfileWebPageUrl = NormalizeExternalUrl(subItem.ProfileWebPageUrl);
        HasProfileWebPageUrl = !IsAllGroup && ProfileWebPageUrl.IsNotEmpty();

        SupportUrl = NormalizeExternalUrl(subItem.SupportUrl);
        HasSupportUrl = !IsAllGroup && SupportUrl.IsNotEmpty();

        ApplyTraffic(subItem, allSummary);
        ApplySubscriptionMeta(subItem, nowLocal);
        ApplyNotesAndExtraConfig(subItem);
        ApplyNodeMetrics(metrics, nowLocal);
    }

    public void ApplyNodeMetrics(SubscriptionNodeMetrics metrics, DateTimeOffset? nowOverride = null)
    {
        var nowLocal = nowOverride ?? DateTimeOffset.Now;

        if (metrics.TotalCount <= 0)
        {
            HasNodeStats = false;
            ShowEmptyNodesHint = true;
            EmptyNodesHintText = IsRemoteSubscription ? "暂无节点 · 点击右上角更新订阅拉取" : "暂无节点";
            NodeCountsText = "可用 0 / 已测 0 / 总数 0";
            MedianDelayText = "中位延迟: 未测速";
            LastSpeedtestText = "最近测速: 尚未测速";
            HealthBarToolTip = "暂无节点";
            FastNodeCount = 0;
            MediumNodeCount = 0;
            FailedNodeCount = 0;
            UntestedSegmentWeight = 1;
            return;
        }

        HasNodeStats = true;
        ShowEmptyNodesHint = false;
        EmptyNodesHintText = string.Empty;

        NodeCountsText = $"可用 {metrics.AvailableCount} / 已测 {metrics.TestedCount} / 总数 {metrics.TotalCount}";
        MedianDelayText = metrics.MedianDelayMs is { } median
            ? $"中位延迟: {median} ms"
            : metrics.TestedCount > 0
                ? "中位延迟: 全部超时"
                : "中位延迟: 未测速";

        var (speedtestText, _) = FormatLastSpeedtest(metrics.LastSpeedtestAt, metrics.TestedCount, nowLocal);
        LastSpeedtestText = speedtestText;

        var untestedCount = Math.Max(0, metrics.TotalCount - metrics.FastCount - metrics.MediumCount - metrics.FailedCount);
        FastNodeCount = Math.Max(0, metrics.FastCount);
        MediumNodeCount = Math.Max(0, metrics.MediumCount);
        FailedNodeCount = Math.Max(0, metrics.FailedCount);
        UntestedSegmentWeight = untestedCount;

        HealthBarToolTip = $"低延迟 (≤{FastDelayThresholdMs}ms): {FastNodeCount} · 较高延迟 (>{FastDelayThresholdMs}ms): {MediumNodeCount} · 超时失败: {FailedNodeCount} · 未测速: {untestedCount}";
    }

    private void ApplyTraffic(SubItem subItem, SubscriptionAllSummary allSummary)
    {
        if (IsAllGroup)
        {
            var aggUp = Math.Max(0L, allSummary.TotalUploadBytes);
            var aggDown = Math.Max(0L, allSummary.TotalDownloadBytes);
            var aggUsed = aggUp + aggDown;
            if (aggUsed > 0)
            {
                HasTrafficSection = true;
                HasQuotaLimit = false;
                ShowUnlimitedTrafficRow = true;
                UnlimitedTrafficText = "流量汇总: 多订阅累计";
                TrafficDetailText = $"已用流量: {FormatBytes(aggUsed)} · ↑ {FormatBytes(aggUp)} · ↓ {FormatBytes(aggDown)}";
            }
            else
            {
                ResetTrafficProperties();
            }
            return;
        }

        var up = Math.Max(0L, subItem.TrafficUpload);
        var down = Math.Max(0L, subItem.TrafficDownload);
        var used = up + down;
        var total = subItem.TrafficTotal;

        if (!IsRemoteSubscription && total <= 0 && used <= 0)
        {
            ResetTrafficProperties();
            return;
        }

        HasTrafficSection = true;
        if (total > 0)
        {
            var remaining = Math.Max(0L, total - used);
            var ratio = Math.Clamp((double)remaining / total, 0d, 1d);
            var percent = (int)Math.Round(ratio * 100d, MidpointRounding.AwayFromZero);

            HasQuotaLimit = true;
            ShowUnlimitedTrafficRow = false;
            QuotaRemainingText = $"剩余流量: {FormatBytes(remaining)}";
            QuotaPercentText = $"{percent}%";
            QuotaPercentValue = ratio * 100d;
            QuotaRemainingRatio = ratio;
            QuotaFilledWeight = Math.Clamp((int)Math.Round(ratio * 1000d, MidpointRounding.AwayFromZero), 0, 1000);
            QuotaEmptyWeight = 1000 - QuotaFilledWeight;
            IsQuotaDanger = ratio <= 0.05d;
            IsQuotaWarning = !IsQuotaDanger && ratio <= 0.20d;
            UnlimitedTrafficText = string.Empty;
            TrafficDetailText = $"已用 {FormatBytes(used)} / 共 {FormatBytes(total)} · ↑ {FormatBytes(up)} · ↓ {FormatBytes(down)}";
        }
        else
        {
            HasQuotaLimit = false;
            ShowUnlimitedTrafficRow = true;
            QuotaRemainingText = string.Empty;
            QuotaPercentText = string.Empty;
            QuotaPercentValue = 0d;
            QuotaRemainingRatio = 0d;
            QuotaFilledWeight = 0;
            QuotaEmptyWeight = 1000;
            IsQuotaDanger = false;
            IsQuotaWarning = false;
            UnlimitedTrafficText = subItem.UpdateTime > 0 || used > 0 ? "流量: 不限流量" : "流量: 待同步";
            TrafficDetailText = $"已用流量: {FormatBytes(used)} · ↑ {FormatBytes(up)} · ↓ {FormatBytes(down)}";
        }
    }

    private void ResetTrafficProperties()
    {
        HasTrafficSection = false;
        HasQuotaLimit = false;
        ShowUnlimitedTrafficRow = false;
        QuotaRemainingText = string.Empty;
        QuotaPercentText = string.Empty;
        QuotaPercentValue = 0d;
        QuotaRemainingRatio = 0d;
        QuotaFilledWeight = 0;
        QuotaEmptyWeight = 1000;
        IsQuotaWarning = false;
        IsQuotaDanger = false;
        UnlimitedTrafficText = string.Empty;
        TrafficDetailText = string.Empty;
    }

    private void ApplySubscriptionMeta(SubItem subItem, DateTimeOffset nowLocal)
    {
        if (IsAllGroup || (!IsRemoteSubscription && subItem.ExpireTime <= 0 && subItem.UpdateTime <= 0))
        {
            HasSubscriptionMeta = false;
            ExpireText = string.Empty;
            IsExpired = false;
            IsExpiringSoon = false;
            SyncTimeText = string.Empty;
            return;
        }

        HasSubscriptionMeta = true;
        var (expireText, isExpired, isExpiringSoon) = FormatExpiration(subItem.ExpireTime, subItem.UpdateTime, nowLocal);
        ExpireText = expireText;
        IsExpired = isExpired;
        IsExpiringSoon = isExpiringSoon;
        SyncTimeText = FormatSyncTime(subItem.UpdateTime, nowLocal.Offset);
    }

    private void ApplyNotesAndExtraConfig(SubItem subItem)
    {
        if (IsAllGroup)
        {
            HasNoteBox = false;
            NoteBoxText = string.Empty;
            NoteBoxToolTip = string.Empty;
            HasExtraConfig = false;
            ExtraConfigText = string.Empty;
            return;
        }

        var announceRaw = subItem.Announce?.Trim() ?? string.Empty;
        var memoRaw = subItem.Memo?.Trim() ?? string.Empty;

        if (announceRaw.IsNotEmpty() && memoRaw.IsNotEmpty())
        {
            HasNoteBox = true;
            NoteBoxText = $"公告: {CollapseWhitespace(announceRaw)}{Environment.NewLine}备注: {CollapseWhitespace(memoRaw)}";
            NoteBoxToolTip = $"公告:{Environment.NewLine}{announceRaw}{Environment.NewLine}{Environment.NewLine}备注:{Environment.NewLine}{memoRaw}";
        }
        else if (announceRaw.IsNotEmpty())
        {
            HasNoteBox = true;
            NoteBoxText = $"公告: {CollapseWhitespace(announceRaw)}";
            NoteBoxToolTip = announceRaw;
        }
        else if (memoRaw.IsNotEmpty())
        {
            HasNoteBox = true;
            NoteBoxText = $"备注: {CollapseWhitespace(memoRaw)}";
            NoteBoxToolTip = memoRaw;
        }
        else
        {
            HasNoteBox = false;
            NoteBoxText = string.Empty;
            NoteBoxToolTip = string.Empty;
        }

        var extraParts = new List<string>(6);
        if (subItem.Filter?.Trim() is { Length: > 0 } filter)
        {
            extraParts.Add($"过滤: {filter}");
        }
        if (subItem.CustomCoreType is { } coreType)
        {
            extraParts.Add($"内核: {coreType}");
        }
        if (subItem.ConvertTarget?.Trim() is { Length: > 0 } convertTarget)
        {
            extraParts.Add($"转换: {convertTarget}");
        }
        if (subItem.PreSocksPort is > 0)
        {
            extraParts.Add($"前置端口: {subItem.PreSocksPort.Value}");
        }
        if (subItem.PrevProfile?.Trim().IsNotEmpty() == true || subItem.NextProfile?.Trim().IsNotEmpty() == true)
        {
            extraParts.Add("链式代理");
        }
        if (subItem.MoreUrl?.Trim() is { Length: > 0 } moreUrl)
        {
            var extraUrlCount = Utils.String2List(moreUrl)?.Count ?? 0;
            if (extraUrlCount > 0)
            {
                extraParts.Add($"+{extraUrlCount} 附加源");
            }
        }

        ExtraConfigText = string.Join(" · ", extraParts);
        HasExtraConfig = ExtraConfigText.IsNotEmpty();
    }

    public static SubscriptionNodeMetrics BuildNodeMetrics(IEnumerable<int> delays, DateTimeOffset? lastSpeedtestAt)
    {
        var total = 0;
        var fast = 0;
        var medium = 0;
        var failed = 0;
        var availableDelays = new List<int>();

        foreach (var delay in delays)
        {
            total++;
            if (delay > 0)
            {
                availableDelays.Add(delay);
                if (delay <= FastDelayThresholdMs)
                {
                    fast++;
                }
                else
                {
                    medium++;
                }
            }
            else if (delay < 0)
            {
                failed++;
            }
        }

        var available = availableDelays.Count;
        var tested = available + failed;
        int? median = null;
        if (available > 0)
        {
            availableDelays.Sort();
            var mid = available / 2;
            median = available % 2 == 1
                ? availableDelays[mid]
                : (int)Math.Round((availableDelays[mid - 1] + availableDelays[mid]) / 2d, MidpointRounding.AwayFromZero);
        }

        return new SubscriptionNodeMetrics(
            TotalCount: total,
            TestedCount: tested,
            AvailableCount: available,
            FastCount: fast,
            MediumCount: medium,
            FailedCount: failed,
            MedianDelayMs: median,
            LastSpeedtestAt: lastSpeedtestAt);
    }

    public static string FormatBytes(long bytes)
    {
        var clamped = Math.Max(0L, bytes);
        if (clamped < 1024L)
        {
            return $"{clamped.ToString(CultureInfo.InvariantCulture)} B";
        }

        double value = clamped;
        var unitIndex = 0;
        while (value >= 1024d && unitIndex < _byteUnits.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {_byteUnits[unitIndex]}";
    }

    public static string FormatAutoUpdateBadge(int autoUpdateIntervalMinutes)
    {
        if (autoUpdateIntervalMinutes <= 0)
        {
            return "手动更新";
        }

        if (autoUpdateIntervalMinutes >= 60)
        {
            var hours = autoUpdateIntervalMinutes / 60;
            var mins = autoUpdateIntervalMinutes % 60;
            return mins == 0
                ? $"自动更新 {hours}h"
                : $"自动更新 {hours}h{mins}m";
        }

        return $"自动更新 {autoUpdateIntervalMinutes}m";
    }

    public static (string Text, bool IsExpired, bool IsExpiringSoon) FormatExpiration(
        long expireUnixTime,
        long updateUnixTime,
        DateTimeOffset nowLocal)
    {
        if (expireUnixTime <= 0)
        {
            return (updateUnixTime > 0 ? "到期时间: 长期有效" : "到期时间: 待同步", false, false);
        }

        if (!TryConvertUnixTimestamp(expireUnixTime, nowLocal.Offset, out var expireLocal))
        {
            return ("到期时间: -", false, false);
        }

        var dateStr = expireLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var remaining = expireLocal - nowLocal;
        if (remaining <= TimeSpan.Zero)
        {
            return ($"到期时间: {dateStr} · 已过期", true, false);
        }

        if (remaining.TotalDays < 1d)
        {
            var hours = Math.Max(1, (int)Math.Ceiling(remaining.TotalHours));
            return ($"到期时间: {dateStr} · 剩余 {hours} 小时", false, true);
        }

        var days = (int)Math.Floor(remaining.TotalDays);
        return ($"到期时间: {dateStr} · 剩余 {days} 天", false, days <= 7);
    }

    public static string FormatSyncTime(long updateUnixTime, TimeSpan localOffset)
    {
        if (updateUnixTime <= 0)
        {
            return "同步时间: 尚未同步";
        }

        return TryConvertUnixTimestamp(updateUnixTime, localOffset, out var syncLocal)
            ? $"同步时间: {syncLocal.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}"
            : "同步时间: -";
    }

    public static (string Text, bool IsStale) FormatLastSpeedtest(
        DateTimeOffset? lastSpeedtestAt,
        int testedCount,
        DateTimeOffset nowLocal)
    {
        if (lastSpeedtestAt is { } ts)
        {
            var localTs = ts.ToOffset(nowLocal.Offset);
            var isStale = (nowLocal - localTs) >= SpeedtestStaleThreshold;
            var formatted = localTs.ToString("yyyy/M/d HH:mm", CultureInfo.InvariantCulture);
            return (isStale ? $"最近测速: {formatted} · 可能过时" : $"最近测速: {formatted}", isStale);
        }

        if (testedCount > 0)
        {
            return ("最近测速: 历史测速 · 可能过时", true);
        }

        return ("最近测速: 尚未测速", false);
    }

    public static string NormalizeExternalUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return uri.Scheme is "http" or "https" or "tg" ? uri.AbsoluteUri : string.Empty;
    }

    private static bool TryConvertUnixTimestamp(long unixTimestamp, TimeSpan targetOffset, out DateTimeOffset result)
    {
        try
        {
            var utc = unixTimestamp > 253_402_300_799L
                ? DateTimeOffset.FromUnixTimeMilliseconds(unixTimestamp)
                : DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            result = utc.ToOffset(targetOffset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
    }

    private static string CollapseWhitespace(string text)
    {
        if (text.IsNullOrEmpty())
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text.Length);
        var prevWhitespace = false;
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!prevWhitespace)
                {
                    sb.Append(' ');
                    prevWhitespace = true;
                }
            }
            else
            {
                sb.Append(ch);
                prevWhitespace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
