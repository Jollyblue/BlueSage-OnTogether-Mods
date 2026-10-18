using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class MiniMapFriendsCompatibilityContractTests
{
    [TestCase(true, 1, 3, true)]
    [TestCase(true, 2, 3, false)]
    [TestCase(false, 1, 3, false)]
    public void Friend_icon_reasserts_last_sibling_after_hierarchy_churn(
        bool isFriend,
        int currentSiblingIndex,
        int siblingCount,
        bool expected)
    {
        Assert.That(
            MiniMapFriendVisualPolicy.ShouldMoveToLastSibling(
                isFriend,
                currentSiblingIndex,
                siblingCount),
            Is.EqualTo(expected));
    }

    [Test]
    public void FriendColorsUseSteamCacheAndBackOffForStandaloneMod()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        string patch = File.ReadAllText(Path.Combine(root, "src", "Patches", "MiniMapPlayerLabelPatch.cs"));
        Assert.That(patch, Does.Contain("com.xaviergmail.minimapfriends"));
        Assert.That(patch, Does.Contain("SteamFriends.GetFriendRelationship"));
        Assert.That(patch, Does.Contain("FriendCache"));
        Assert.That(patch, Does.Contain("RefreshIntervalSeconds = 2f"));
        Assert.That(patch, Does.Contain("icon.SetAsLastSibling()"));
    }
}
