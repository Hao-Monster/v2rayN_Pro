using TUnit.Assertions.Should;
using TUnit.Assertions.Should.Extensions;

namespace ServiceLib.Tests.Handler;

public class UiLayoutMigrationTests
{
    [Test]
    public async Task ApplyUiLayoutMigration_LegacyUnmigratedConfig_MigratesToTabOnce()
    {
        var uiItem = new UIItem
        {
            MainGirdOrientation = EGirdOrientation.Vertical,
            LeftRailLayoutMigrated = false,
            MainWorkspacePage = EMainWorkspacePage.Home,
        };

        var changed = ConfigHandler.ApplyUiLayoutMigration(uiItem);

        await changed.Should().BeTrue();
        await uiItem.MainGirdOrientation.Should().BeEqualTo(EGirdOrientation.Tab);
        await uiItem.LeftRailLayoutMigrated.Should().BeTrue();
    }

    [Test]
    public async Task ApplyUiLayoutMigration_AlreadyMigratedConfig_PreservesUserSplitOrientation()
    {
        var uiItem = new UIItem
        {
            MainGirdOrientation = EGirdOrientation.Horizontal,
            LeftRailLayoutMigrated = true,
            MainWorkspacePage = EMainWorkspacePage.Logs,
        };

        var changed = ConfigHandler.ApplyUiLayoutMigration(uiItem);

        await changed.Should().BeFalse();
        await uiItem.MainGirdOrientation.Should().BeEqualTo(EGirdOrientation.Horizontal);
        await uiItem.MainWorkspacePage.Should().BeEqualTo(EMainWorkspacePage.Logs);
    }

    [Test]
    public async Task ApplyUiLayoutMigration_InvalidOrTransientWorkspacePage_NormalizesSafely()
    {
        var invalidPageItem = new UIItem
        {
            LeftRailLayoutMigrated = true,
            MainWorkspacePage = (EMainWorkspacePage)99,
        };
        var transientClashItem = new UIItem
        {
            LeftRailLayoutMigrated = true,
            MainWorkspacePage = EMainWorkspacePage.ClashConnections,
        };

        var invalidChanged = ConfigHandler.ApplyUiLayoutMigration(invalidPageItem);
        var clashChanged = ConfigHandler.ApplyUiLayoutMigration(transientClashItem);

        await invalidChanged.Should().BeTrue();
        await invalidPageItem.MainWorkspacePage.Should().BeEqualTo(EMainWorkspacePage.Home);
        await clashChanged.Should().BeTrue();
        await transientClashItem.MainWorkspacePage.Should().BeEqualTo(EMainWorkspacePage.Proxy);
    }

    [Test]
    public async Task ApplyUiLayoutMigration_NullOrRepeatedInvocation_IsNullSafeAndIdempotent()
    {
        var nullChanged = ConfigHandler.ApplyUiLayoutMigration(null);

        var uiItem = new UIItem
        {
            MainGirdOrientation = EGirdOrientation.Horizontal,
            LeftRailLayoutMigrated = false,
            MainWorkspacePage = EMainWorkspacePage.Proxy,
        };
        var firstChanged = ConfigHandler.ApplyUiLayoutMigration(uiItem);
        var secondChanged = ConfigHandler.ApplyUiLayoutMigration(uiItem);

        await nullChanged.Should().BeFalse();
        await firstChanged.Should().BeTrue();
        await secondChanged.Should().BeFalse();
        await uiItem.MainGirdOrientation.Should().BeEqualTo(EGirdOrientation.Tab);
    }
}
