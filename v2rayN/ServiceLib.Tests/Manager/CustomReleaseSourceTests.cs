using AwesomeAssertions;
using ServiceLib.Enums;
using ServiceLib.Manager;
using TUnit.Core;

namespace ServiceLib.Tests.Manager;

public class CustomReleaseSourceTests
{
    [Test]
    public void GuiUpdate_ShouldUseCustomReleaseRepository()
    {
        var app = CoreInfoManager.Instance.GetCoreInfo(ECoreType.v2rayN);

        app.Should().NotBeNull();
        app!.Url.Should().Be("https://github.com/FengHaoyun-MONSTER/v2rayN_Pro/releases");
        app.DownloadUrlWin64.Should().Contain("FengHaoyun-MONSTER/v2rayN_Pro");
        app.DownloadUrlOSXArm64.Should().Contain("FengHaoyun-MONSTER/v2rayN_Pro");
    }

    [Test]
    public void CoreUpdates_ShouldKeepOfficialRepositories()
    {
        var xray = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray);
        var mihomo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.mihomo);
        var singBox = CoreInfoManager.Instance.GetCoreInfo(ECoreType.sing_box);

        xray!.Url.Should().Contain("XTLS/Xray-core");
        mihomo!.Url.Should().Contain("MetaCubeX/mihomo");
        singBox!.Url.Should().Contain("SagerNet/sing-box");
    }
}
