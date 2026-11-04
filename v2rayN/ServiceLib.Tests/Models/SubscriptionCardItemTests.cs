using TUnit.Assertions.Should;
using TUnit.Assertions.Should.Extensions;
using TUnit.Core;

namespace ServiceLib.Tests.Models;

public class SubscriptionCardItemTests
{
    [Test]
    public async Task FormatBytes_FormatsZeroSubKilobyteExactAndFractionalUnits()
    {
        await SubscriptionCardItem.FormatBytes(-100).Should().BeEqualTo("0 B");
        await SubscriptionCardItem.FormatBytes(0).Should().BeEqualTo("0 B");
        await SubscriptionCardItem.FormatBytes(512).Should().BeEqualTo("512 B");
        await SubscriptionCardItem.FormatBytes(500L * 1024 * 1024 * 1024).Should().BeEqualTo("500 GB");
        await SubscriptionCardItem.FormatBytes(688_784_556_032L).Should().BeEqualTo("641.48 GB");
    }

    [Test]
    public async Task FormatAutoUpdateBadge_FormatsManualMinutesHoursAndMixedIntervals()
    {
        await SubscriptionCardItem.FormatAutoUpdateBadge(0).Should().BeEqualTo("手动更新");
        await SubscriptionCardItem.FormatAutoUpdateBadge(-5).Should().BeEqualTo("手动更新");
        await SubscriptionCardItem.FormatAutoUpdateBadge(30).Should().BeEqualTo("自动更新 30m");
        await SubscriptionCardItem.FormatAutoUpdateBadge(60).Should().BeEqualTo("自动更新 1h");
        await SubscriptionCardItem.FormatAutoUpdateBadge(1440).Should().BeEqualTo("自动更新 24h");
        await SubscriptionCardItem.FormatAutoUpdateBadge(90).Should().BeEqualTo("自动更新 1h30m");
    }

    [Test]
    public async Task FormatExpiration_HandlesLongTermPendingCountdownAndExpiredStates()
    {
        var offset = TimeSpan.FromHours(8);
        var now = new DateTimeOffset(2026, 11, 2, 22, 2, 0, offset);
        var syncedAt = now.ToUnixTimeSeconds();

        var longTerm = SubscriptionCardItem.FormatExpiration(0, syncedAt, now);
        await longTerm.Text.Should().BeEqualTo("到期时间: 长期有效");
        await longTerm.IsExpired.Should().BeFalse();
        await longTerm.IsExpiringSoon.Should().BeFalse();

        var pending = SubscriptionCardItem.FormatExpiration(0, 0, now);
        await pending.Text.Should().BeEqualTo("到期时间: 待同步");

        var future23Days = new DateTimeOffset(2026, 11, 26, 0, 0, 0, offset).ToUnixTimeSeconds();
        var active = SubscriptionCardItem.FormatExpiration(future23Days, syncedAt, now);
        await active.Text.Should().BeEqualTo("到期时间: 2026-11-26 · 剩余 23 天");
        await active.IsExpired.Should().BeFalse();
        await active.IsExpiringSoon.Should().BeFalse();

        var future6Hours = now.AddHours(5.2).ToUnixTimeSeconds();
        var soon = SubscriptionCardItem.FormatExpiration(future6Hours, syncedAt, now);
        await soon.Text.Should().Contain("剩余 6 小时");
        await soon.IsExpired.Should().BeFalse();
        await soon.IsExpiringSoon.Should().BeTrue();

        var past = now.AddDays(-1).ToUnixTimeSeconds();
        var expired = SubscriptionCardItem.FormatExpiration(past, syncedAt, now);
        await expired.Text.Should().BeEqualTo("到期时间: 2026-11-01 · 已过期");
        await expired.IsExpired.Should().BeTrue();
    }

    [Test]
    public async Task BuildNodeMetrics_ComputesBucketsAndOddEvenMedians()
    {
        var oddMetrics = SubscriptionCardItem.BuildNodeMetrics([187, 193, 230, 237, 254, 255, 259, 520, 680, -1, -1, 0], null);
        await oddMetrics.TotalCount.Should().BeEqualTo(12);
        await oddMetrics.AvailableCount.Should().BeEqualTo(9);
        await oddMetrics.TestedCount.Should().BeEqualTo(11);
        await oddMetrics.FastCount.Should().BeEqualTo(7);
        await oddMetrics.MediumCount.Should().BeEqualTo(2);
        await oddMetrics.FailedCount.Should().BeEqualTo(2);
        await oddMetrics.MedianDelayMs.Should().BeEqualTo(254);

        var evenMetrics = SubscriptionCardItem.BuildNodeMetrics([100, 200, 314, 320, 510, 600], null);
        await evenMetrics.MedianDelayMs.Should().BeEqualTo(317);

        var failedOnly = SubscriptionCardItem.BuildNodeMetrics([-1, -1, 0], null);
        await failedOnly.AvailableCount.Should().BeEqualTo(0);
        await failedOnly.TestedCount.Should().BeEqualTo(2);
        await failedOnly.MedianDelayMs.Should().BeNull();
    }

    [Test]
    public async Task Apply_UnlimitedRemoteSubscription_PopulatesCompleteCardDisplay()
    {
        var offset = TimeSpan.FromHours(8);
        var now = new DateTimeOffset(2026, 11, 5, 0, 10, 0, offset);
        var syncAt = new DateTimeOffset(2026, 11, 5, 0, 5, 0, offset).ToUnixTimeSeconds();
        var speedtestAt = new DateTimeOffset(2026, 11, 4, 22, 13, 0, offset);

        var sub = new SubItem
        {
            Id = "sub-admin",
            Remarks = "admin",
            Url = "https://example.com/api/v1/client/subscribe?token=secret",
            Enabled = true,
            AutoUpdateInterval = 60,
            UpdateTime = syncAt,
            TrafficUpload = 0,
            TrafficDownload = 688_784_556_032L,
            TrafficTotal = 0,
            ExpireTime = 0,
            Announce = "分享送流量.\n不好用的时候,要学会自己更新订阅切换节点哦",
            ProfileWebPageUrl = "https://portal.example.com",
            SupportUrl = "javascript:alert(1)"
        };

        var delays = Enumerable.Repeat(280, 19)
            .Concat(Enumerable.Repeat(354, 13))
            .Concat(Enumerable.Repeat(620, 6))
            .Concat(Enumerable.Repeat(-1, 2));
        var metrics = SubscriptionCardItem.BuildNodeMetrics(delays, speedtestAt);

        var card = new SubscriptionCardItem();
        card.Apply(sub, metrics, isActiveConfig: true, nowOverride: now);

        await card.IsRemoteSubscription.Should().BeTrue();
        await card.IsActiveConfig.Should().BeTrue();
        await card.ShowUpdateButton.Should().BeTrue();
        await card.CanUpdateSubscription.Should().BeTrue();
        await card.UpdateButtonText.Should().BeEqualTo("更新订阅");
        await card.UpdateModeBadgeText.Should().BeEqualTo("自动更新 1h");
        await card.HasTrafficSection.Should().BeTrue();
        await card.HasQuotaLimit.Should().BeFalse();
        await card.ShowUnlimitedTrafficRow.Should().BeTrue();
        await card.UnlimitedTrafficText.Should().BeEqualTo("流量: 不限流量");
        await card.TrafficDetailText.Should().BeEqualTo("已用流量: 641.48 GB · ↑ 0 B · ↓ 641.48 GB");
        await card.ExpireText.Should().BeEqualTo("到期时间: 长期有效");
        await card.SyncTimeText.Should().BeEqualTo("同步时间: 2026-11-05 00:05");
        await card.HasNoteBox.Should().BeTrue();
        await card.NoteBoxText.Should().BeEqualTo("公告: 分享送流量. 不好用的时候,要学会自己更新订阅切换节点哦");
        await card.HasProfileWebPageUrl.Should().BeTrue();
        await card.HasSupportUrl.Should().BeFalse();
        await card.NodeCountsText.Should().BeEqualTo("可用 38 / 已测 40 / 总数 40");
        await card.MedianDelayText.Should().BeEqualTo("中位延迟: 317 ms");
        await card.LastSpeedtestText.Should().BeEqualTo("最近测速: 2026/11/4 22:13 · 可能过时");
        await card.FastNodeCount.Should().BeEqualTo(32);
        await card.MediumNodeCount.Should().BeEqualTo(6);
        await card.FailedNodeCount.Should().BeEqualTo(2);
        await card.UntestedSegmentWeight.Should().BeEqualTo(0);
    }

    [Test]
    public async Task Apply_LimitedQuotaAndExtraConfig_ComputesRemainingPercentageAndTags()
    {
        var offset = TimeSpan.FromHours(8);
        var now = new DateTimeOffset(2026, 11, 5, 1, 0, 0, offset);
        var total = 100L * 1024 * 1024 * 1024;
        var up = 35L * 1024 * 1024 * 1024;
        var down = 50L * 1024 * 1024 * 1024;

        var sub = new SubItem
        {
            Id = "sub-limited",
            Remarks = "测试套餐",
            Url = "https://example.com/sub",
            Enabled = false,
            AutoUpdateInterval = 1440,
            UpdateTime = now.AddMinutes(-10).ToUnixTimeSeconds(),
            TrafficTotal = total,
            TrafficUpload = up,
            TrafficDownload = down,
            Filter = "HK|SG",
            CustomCoreType = ECoreType.sing_box,
            MoreUrl = "https://backup1.example.com/sub,https://backup2.example.com/sub",
            Memo = "主力备用线路"
        };

        var metrics = SubscriptionCardItem.BuildNodeMetrics([120, 180], now.AddMinutes(-5));
        var card = new SubscriptionCardItem();
        card.Apply(sub, metrics, isActiveConfig: false, nowOverride: now);

        await card.IsDisabled.Should().BeTrue();
        await card.CanUpdateSubscription.Should().BeFalse();
        await card.HasQuotaLimit.Should().BeTrue();
        await card.QuotaRemainingText.Should().BeEqualTo("剩余流量: 15 GB");
        await card.QuotaPercentText.Should().BeEqualTo("15%");
        await card.IsQuotaWarning.Should().BeTrue();
        await card.IsQuotaDanger.Should().BeFalse();
        await card.QuotaFilledWeight.Should().BeEqualTo(150);
        await card.QuotaEmptyWeight.Should().BeEqualTo(850);
        await card.TrafficDetailText.Should().BeEqualTo("已用 85 GB / 共 100 GB · ↑ 35 GB · ↓ 50 GB");
        await card.HasNoteBox.Should().BeTrue();
        await card.NoteBoxText.Should().BeEqualTo("备注: 主力备用线路");
        await card.HasExtraConfig.Should().BeTrue();
        await card.ExtraConfigText.Should().Contain("过滤: HK|SG");
        await card.ExtraConfigText.Should().Contain("内核: sing_box");
        await card.ExtraConfigText.Should().Contain("+2 附加源");
        await card.LastSpeedtestText.Should().BeEqualTo("最近测速: 2026/11/5 00:55");
    }

    [Test]
    public async Task Apply_AllGroupSummaryCard_AggregatesSubscriptionsAndNodes()
    {
        var offset = TimeSpan.FromHours(8);
        var now = new DateTimeOffset(2026, 11, 5, 1, 0, 0, offset);
        var allSub = new SubItem { Remarks = "所有" };
        var summary = new SubscriptionAllSummary(
            RemoteSubscriptionCount: 2,
            EnabledRemoteSubscriptionCount: 2,
            TotalUploadBytes: 2L * 1024 * 1024 * 1024,
            TotalDownloadBytes: 8L * 1024 * 1024 * 1024);
        var metrics = SubscriptionCardItem.BuildNodeMetrics([95, 210, -1, 0], now);

        var card = new SubscriptionCardItem();
        card.Apply(allSub, metrics, isActiveConfig: true, allSummary: summary, nowOverride: now);

        await card.IsAllGroup.Should().BeTrue();
        await card.ShowUpdateButton.Should().BeTrue();
        await card.UpdateButtonText.Should().BeEqualTo("更新全部");
        await card.UpdateModeBadgeText.Should().BeEqualTo("汇总 2 个订阅");
        await card.HasTrafficSection.Should().BeTrue();
        await card.TrafficDetailText.Should().BeEqualTo("已用流量: 10 GB · ↑ 2 GB · ↓ 8 GB");
        await card.HasSubscriptionMeta.Should().BeFalse();
        await card.NodeCountsText.Should().BeEqualTo("可用 2 / 已测 3 / 总数 4");
        await card.UntestedSegmentWeight.Should().BeEqualTo(1);
    }
}

