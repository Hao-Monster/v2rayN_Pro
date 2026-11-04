using AwesomeAssertions;
using ServiceLib.Models.Dto;
using TUnit.Core;

namespace ServiceLib.Tests.Models;

public class SemanticVersionTests
{
    [Test]
    public void CustomRevision_ShouldBeNewerThanItsUpstreamBase()
    {
        var custom = new SemanticVersion("v7.23.4-custom.1");
        var upstream = new SemanticVersion("7.23.4");

        (custom >= upstream).Should().BeTrue();
        custom.Should().NotBe(upstream);
    }

    [Test]
    public void LaterCustomRevision_ShouldWin()
    {
        var current = new SemanticVersion("7.23.4-custom.1");
        var update = new SemanticVersion("7.23.4-custom.2");

        (current >= update).Should().BeFalse();
        (current <= update).Should().BeTrue();
    }

    [Test]
    public void NewerUpstreamBase_ShouldWinOverCustomRevision()
    {
        var current = new SemanticVersion("7.23.4-custom.99");
        var update = new SemanticVersion("7.24.0-custom.1");

        (current >= update).Should().BeFalse();
    }

    [Test]
    public void FourPartNumericVersion_ShouldRemainSupported()
    {
        var current = new SemanticVersion("7.23.4.1");
        var update = new SemanticVersion("7.23.4.2");

        (current >= update).Should().BeFalse();
    }
}
