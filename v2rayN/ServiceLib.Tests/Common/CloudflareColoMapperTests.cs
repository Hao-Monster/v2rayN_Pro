using AwesomeAssertions;
using TUnit.Core;

namespace ServiceLib.Tests.Common;

public class CloudflareColoMapperTests
{
    [Test]
    public void FromTrace_ShouldMapKnownColo()
    {
        const string trace = "fl=123f456\r\nh=speed.cloudflare.com\r\ncolo=SJC\r\nloc=US\r\n";

        CloudflareColoMapper.FromTrace(trace).Should().Be("美国·圣何塞");
    }

    [Test]
    public void FromTrace_ShouldUseCountryForUnknownColo()
    {
        const string trace = "colo=XYZ\nloc=SG\n";

        CloudflareColoMapper.FromTrace(trace).Should().Be("新加坡·XYZ");
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("loc=ZZ")]
    public void FromTrace_ShouldReturnUnknownWhenNoUsableLocationExists(string? trace)
    {
        CloudflareColoMapper.FromTrace(trace).Should().Be(CloudflareColoMapper.Unknown);
    }
}
