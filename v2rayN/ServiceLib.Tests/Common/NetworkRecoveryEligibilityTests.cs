using AwesomeAssertions;
using TUnit.Core;

namespace ServiceLib.Tests.Common;

public class NetworkRecoveryEligibilityTests
{
    [Test]
    [Arguments(0, 0, false)]
    [Arguments(1, 0, false)]
    [Arguments(0, 1, false)]
    [Arguments(1, 1, true)]
    [Arguments(3, 20, true)]
    public void CanRun_RequiresSubscriptionsAndProfiles(
        int subscriptionCount,
        int profileCount,
        bool expected)
    {
        NetworkRecoveryEligibility
            .CanRun(subscriptionCount, profileCount)
            .Should()
            .Be(expected);
    }
}
