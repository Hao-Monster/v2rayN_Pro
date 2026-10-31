using AwesomeAssertions;
using TUnit.Core;

namespace ServiceLib.Tests.Common;

public class LatencySortHelperTests
{
    [Test]
    public void OrderAscending_PutsFailedLatencyLast()
    {
        int[] delays = [220, -1, 80, 500, -1, 0];

        var result = LatencySortHelper.OrderAscending(delays, delay => delay).ToArray();

        result.Should().Equal(80, 220, 500, 0, -1, -1);
    }

    [Test]
    [Arguments(true, 200, 100, true)]
    [Arguments(true, 100, 200, false)]
    [Arguments(true, -1, 300, true)]
    [Arguments(false, -1, 300, true)]
    [Arguments(false, -1, -1, false)]
    public void ShouldSwitchToCandidate_UsesCurrentAvailabilityAndLatency(
        bool currentExists,
        int currentDelay,
        int candidateDelay,
        bool expected)
    {
        LatencySortHelper
            .ShouldSwitchToCandidate(currentExists, currentDelay, candidateDelay)
            .Should()
            .Be(expected);
    }
}
