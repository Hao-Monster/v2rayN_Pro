using AwesomeAssertions;
using ServiceLib.Common;
using TUnit.Core;

namespace ServiceLib.Tests.Common;

public class UnixShellResourceTests
{
    [Test]
    [Arguments(Global.ProxySetOSXShellFileName)]
    [Arguments(Global.ProxySetLinuxShellFileName)]
    [Arguments(Global.KillAsSudoOSXShellFileName)]
    [Arguments(Global.KillAsSudoLinuxShellFileName)]
    public void EmbeddedShellResource_ShouldContainOnlyLfLineEndings(string resourceName)
    {
        var contents = EmbedUtils.GetEmbedText(resourceName);

        contents.Should().StartWith("#!/bin/bash\n");
        contents.Should().NotContain("\r");
    }

    [Test]
    public void MacOsSystemProxyScript_ShouldUseAbsoluteNetworkSetupAndReportState()
    {
        var contents = EmbedUtils.GetEmbedText(Global.ProxySetOSXShellFileName);

        contents.Should().Contain("/usr/sbin/networksetup");
        contents.Should().Contain("-getwebproxy");
        contents.Should().Contain("-getsecurewebproxy");
        contents.Should().Contain("-getsocksfirewallproxy");
    }
}