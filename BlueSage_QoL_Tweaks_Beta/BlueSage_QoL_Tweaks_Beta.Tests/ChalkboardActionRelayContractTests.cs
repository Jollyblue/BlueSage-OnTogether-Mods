using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChalkboardActionRelayContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Helper_clear_and_restore_use_a_dedicated_non_chat_member_data_channel_and_host_revalidation()
    {
        string controller = Read("src", "ChalkboardPersistenceController.cs");
        string policy = Read("src", "Core", "ChalkboardModerationPolicy.cs");

        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Contain("bluesage_chalk_request_v2"));
            Assert.That(policy, Does.Contain("BuildRestoreMemberData"));
            Assert.That(policy, Does.Contain("MaxPayloadBytes"));
            Assert.That(controller, Does.Contain(
                "SetLobbyMemberData(lobby, ChalkboardModerationPolicy.MemberDataKey, payload)"));
            Assert.That(controller, Does.Contain("GetLobbyMemberData("));
            Assert.That(controller, Does.Contain("ChalkboardModerationPolicy.MemberDataKey"));
            Assert.That(controller, Does.Contain("TryGetOwnedHostLobby"));

            Assert.That(controller, Does.Contain(
                "PlayerIdentityEvidenceController.TryResolveExact(senderSteamId"));
            Assert.That(controller, Does.Contain("CountLobbyMembers(lobby, senderSteamId) != 1"));
            Assert.That(controller, Does.Contain(
                "SteamIdModerationController.IsAuthorizedCurrentIssuer(senderSteamId)"));
            Assert.That(controller, Does.Contain("ChalkboardModerationPolicy.IsFresh"));

            Assert.That(controller, Does.Contain("RememberClearNonce(request.Nonce)"));
            Assert.That(controller, Does.Contain("LastClearRequestByIssuer.TryGetValue"));
            Assert.That(controller, Does.Contain("now - lastRequest < 2"));
            Assert.That(controller, Does.Contain("LastClearRequestByIssuer[senderSteamId] = now"));

            Assert.That(controller, Does.Contain(
                "!string.Equals(currentFingerprint, expectedFingerprint, StringComparison.Ordinal)"));
            Assert.That(controller, Does.Contain("if (!board.isServer)"));
            Assert.That(controller, Does.Contain("request.Action, \"load\""));
            Assert.That(controller, Does.Contain("request.Action, \"clear\""));
            Assert.That(controller, Does.Contain("request.SnapshotName"));

            Assert.That(controller, Does.Not.Contain("ModerationProtocolPolicy.MemberDataKey"));
            Assert.That(controller, Does.Not.Contain("ModerationRelayPatch"));
            Assert.That(controller, Does.Not.Contain("SendChat"));
            Assert.That(controller, Does.Not.Contain("SendMessage("));
            Assert.That(controller, Does.Not.Contain("BanData"));
            Assert.That(controller, Does.Not.Contain("BanRPC"));
            Assert.That(controller, Does.Not.Contain("CommunityBanListController"));
        });
    }

    [Test]
    public void General_moderation_member_data_rejects_chalk_clear_actions()
    {
        const string steamId = "76561198012345678";
        const long issued = 1700000000;
        const string nonce = "ab12CD34";

        string built = ModerationProtocolPolicy.BuildMemberData(
            "chalk-clear",
            steamId,
            "none",
            issued,
            nonce);
        string forged = ModerationProtocolPolicy.MemberDataPrefix +
                        "chalk-clear|" + steamId + "|none|" + issued + "|" + nonce;

        Assert.Multiple(() =>
        {
            Assert.That(built, Is.Empty);
            Assert.That(
                ModerationProtocolPolicy.TryParseMemberData(forged, out _),
                Is.False);
        });
    }

    [Test]
    public void Board_set_changes_reset_replay_rate_limit_and_clear_cooldown_state()
    {
        string controller = Read("src", "ChalkboardPersistenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("_boardSetSignature"));
            Assert.That(controller, Does.Contain("RefreshBoardSetState("));
            Assert.That(controller, Does.Contain("BuildBoardSetSignature("));
            Assert.That(controller, Does.Contain("ResetRelayAndCooldownState();"));
            Assert.That(controller, Does.Contain("SeenClearNonces.Clear();"));
            Assert.That(controller, Does.Contain("ClearNonceOrder.Clear();"));
            Assert.That(controller, Does.Contain("LastClearRequestByIssuer.Clear();"));
            Assert.That(controller, Does.Contain("LastClearByBoard.Clear();"));
            Assert.That(controller, Does.Contain("_lastListedBoardSetSignature"));
            Assert.That(controller, Does.Contain("_lastListedBoardSetUnixSeconds"));
            Assert.That(controller, Does.Contain("_rollbackUnresolved"));
            Assert.That(
                CountOccurrences(controller, "_lastSharedRestoreUtc = DateTime.MinValue;"),
                Is.GreaterThanOrEqualTo(2),
                "The declaration and the shared reset path must both reset restore cooldown.");
        });
    }

    [Test]
    public void Shared_clear_reports_partial_verified_recipient_delivery_without_broadening_recipients()
    {
        string controller = Read("src", "ChalkboardPersistenceController.cs");
        string moderationController = Read("src", "ChalkboardModerationController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("ChalkboardDeliveryResult"));
            Assert.That(controller, Does.Contain("attemptedRecipients"));
            Assert.That(controller, Does.Contain("sentRecipients"));
            Assert.That(controller, Does.Contain("skippedRecipients"));
            Assert.That(controller, Does.Contain("ChalkboardClearResult.Partial"));
            Assert.That(controller, Does.Contain("retry-safe"));
            Assert.That(controller, Does.Contain("PlayerIdentityEvidenceController.GetVerifiedRoster()"));
            Assert.That(controller, Does.Contain("ChalkboardModerationController.IsCompatibleRecipient"));
            Assert.That(moderationController, Does.Contain("CompatibleClientPresencePolicy.IsFresh"));
            Assert.That(moderationController, Does.Contain("GetLobbyMemberData"));
            Assert.That(controller, Does.Not.Contain("foreach (PlayerID player in panel.PlayerIDs)"));
        });
    }

    [Test]
    public void Host_execution_paths_keep_destructive_actions_locked_after_unresolved_rollback()
    {
        string controller = Read("src", "ChalkboardPersistenceController.cs");
        string loadPath = Slice(
            controller,
            "private static ChalkboardClearResult ExecuteLoadAsHost(",
            "internal static string List()");
        string clearPath = Slice(
            controller,
            "private static ChalkboardClearResult ExecuteClearAsHost(",
            "private static ChalkboardDeliveryResult ApplyGridAndSync(");

        Assert.Multiple(() =>
        {
            Assert.That(loadPath, Does.Contain(
                "_rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked"));
            Assert.That(clearPath, Does.Contain(
                "_rollbackUnresolved || ChalkboardSnapshotTransportController.DestructiveActionsBlocked"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static int CountOccurrences(string value, string fragment)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static string Slice(string value, string start, string end)
    {
        int startIndex = value.IndexOf(start, StringComparison.Ordinal);
        int endIndex = value.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), "Missing source start marker: " + start);
        Assert.That(endIndex, Is.GreaterThan(startIndex), "Missing source end marker: " + end);
        return value.Substring(startIndex, endIndex - startIndex);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
