using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChalkboardPersistenceContractTests
{
    private static string Read(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        return File.ReadAllText(Path.Combine(root, relative));
    }

    [Test]
    public void FeatureIsDefaultOnHostAuthoritativeReversibleAndChalkyCompatible()
    {
        string plugin = Read("src/Plugin.cs");
        string menu = Read("src/QolMenuWindow.cs");
        string controller = Read("src/ChalkboardPersistenceController.cs");
        string typeahead = Read("src/CommandTypeaheadController.cs");
        string moderation = Read("src/SteamIdModerationController.cs");
        string snapshotPatch = Read("src/Patches/ChalkboardSnapshotPatch.cs");
        string snapshotTransport = Read("src/ChalkboardSnapshotTransportController.cs");
        Assert.That(plugin, Does.Contain("EnableChalkboardPersistence = Config.Bind(\"Chalkboards\", \"EnableChalkboardPersistence\", true"));
        Assert.That(menu, Does.Contain("DrawToggle(\"Chalkboard Recovery\""));
        Assert.That(menu, Does.Contain("Keeps local recovery copies"));
        Assert.That(menu, Does.Contain("Only the Host or an authenticated Helper"));
        Assert.That(menu, Does.Not.Contain("compatible-recipient and vanilla-observer truth"));
        Assert.That(menu, Does.Not.Contain("EXPERIMENTAL - Chalkboard Persistence"));
        Assert.That(controller, Does.Contain("com.andrewlin.ontogether.chalky"));
        Assert.That(controller, Does.Contain("if (!board.isServer)"));
        Assert.That(controller, Does.Contain("File.Replace(temp, target, backup"));
        Assert.That(controller, Does.Contain("GetQuadImage_Original_1"));
        Assert.That(controller, Does.Contain("board.GetQuadImage(player, board.PaintColors)"));
        Assert.That(controller, Does.Contain("TotalSeconds < 8d"));
        Assert.That(plugin, Does.Contain("\"chalk\""));
        Assert.That(plugin, Does.Contain("HandleChalkCommand"));
        Assert.That(controller, Does.Contain("internal static string Boards()"));
        Assert.That(controller, Does.Contain("internal static string TryClear(string boardToken)"));
        Assert.That(controller, Does.Contain("internal static void PollHostMemberDataRequests()"));
        Assert.That(controller, Does.Contain("ChalkboardModerationPolicy.MemberDataKey"));
        Assert.That(controller, Does.Contain("SteamIdModerationController.CanLocalIssue"));
        Assert.That(controller, Does.Contain("SteamIdModerationController.IsAuthorizedCurrentIssuer"));
        Assert.That(controller, Does.Contain("TrySaveResolved(backupName, board, index"));
        Assert.That(controller, Does.Contain("ClassifyBuild24259292Location"));
        Assert.That(snapshotTransport, Does.Contain("GL.Clear"));
        Assert.That(snapshotTransport, Does.Contain("_pixelsToUpdate"));
        Assert.That(snapshotTransport, Does.Contain("pendingPixels.Clear()"));
        Assert.That(controller, Does.Contain("PlayerIdentityEvidenceController.GetVerifiedRoster()"));
        Assert.That(controller, Does.Contain("evidence.NetworkPlayerId"));
        Assert.That(controller, Does.Not.Contain("foreach (PlayerID player in panel.PlayerIDs)"));
        Assert.That(controller, Does.Contain("no authoritative drawing-owner identity"));
        Assert.That(typeahead, Does.Contain("ChalkboardChoices"));
        Assert.That(moderation, Does.Contain("NotifyModerationBoardReview"));
        Assert.That(snapshotPatch, Does.Contain("[HarmonyPatch(typeof(QuadPainterGPU), \"GetQuadImage_Original_1\")]"));
        Assert.That(snapshotPatch, Does.Contain("ChalkboardSnapshotTransportController.PrepareForFullSnapshot"));
    }
}
