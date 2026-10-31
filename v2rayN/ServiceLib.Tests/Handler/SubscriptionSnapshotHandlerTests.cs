using AwesomeAssertions;
using TUnit.Core;

namespace ServiceLib.Tests.Handler;

public class SubscriptionSnapshotHandlerTests
{
    [Test]
    public void GetSnapshotFileName_IsDeterministicAndPathSafe()
    {
        const string hostileSubscriptionId = "../subscription/../../escape";

        var first = SubscriptionSnapshotHandler.GetSnapshotFileName(hostileSubscriptionId);
        var second = SubscriptionSnapshotHandler.GetSnapshotFileName(hostileSubscriptionId);

        first.Should().Be(second);
        first.Should().MatchRegex("^[a-f0-9]{64}\\.json$");
        Path.GetFileName(first).Should().Be(first);
    }

    [Test]
    public void SnapshotDto_RoundTripsThroughApplicationJsonSerializer()
    {
        var snapshot = new SubscriptionSnapshot
        {
            SubId = "subscription-1",
            ActiveIndexId = "node-1",
            CreatedAt = 123,
            Profiles = [new ProfileItem { IndexId = "node-1", Subid = "subscription-1" }],
            ProfileExs = [new ProfileExItem { IndexId = "node-1", Delay = 88 }]
        };

        var restored = JsonUtils.Deserialize<SubscriptionSnapshot>(JsonUtils.Serialize(snapshot));

        restored.Should().NotBeNull();
        restored!.SubId.Should().Be(snapshot.SubId);
        restored.Profiles.Should().ContainSingle(item => item.IndexId == "node-1");
        restored.ProfileExs.Should().ContainSingle(item => item.Delay == 88);
    }
}
