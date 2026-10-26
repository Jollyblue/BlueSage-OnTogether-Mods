using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Runtime wiring for SteamID truth (2026-10-17). Unity/Steam/PurrNet calls cannot run under NUnit,
// so these pin the glue by source; SteamIdTruthPolicyTests and SteamIdTruthAttestationPolicyTests
// hold the behavior.
public sealed class SteamIdTruthWiringContractTests
{
    [Test]
    public void Transport_SID_is_read_only_through_the_host_guarded_reader()
    {
        string reader = ReadSource("SteamIdTruthReader.cs");
        string evidence = ReadSource("PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(reader, Does.Contain("PurrSteamUtils.TryGetSteamID("));
            Assert.That(reader, Does.Contain("manager.isServer"));
            Assert.That(reader, Does.Contain("manager.transport is SteamTransport"));
            Assert.That(evidence, Does.Not.Contain("PurrSteamUtils"));
            Assert.That(evidence, Does.Contain("SteamIdTruthReader.ReadTransportSteamId("));
        });
    }

    [Test]
    public void Local_row_is_identified_by_the_local_network_transform_never_by_the_claimed_string_alone()
    {
        string loop = Slice(ReadSource("PlayerIdentityEvidenceController.cs"), "var result = new List<PlayerIdentityEvidence>();", "snapshot.Verified = result");

        string capture = ReadSource("PlayerIdentityEvidenceController.cs");
        string localRule = Slice(capture, "localRows[index] =", ";");

        Assert.Multiple(() =>
        {
            // Claim AND own network object: the claimed string alone can never make a row "self".
            Assert.That(localRule, Does.StartWith("localRows[index] =\n"));
            Assert.That(localRule, Does.Contain("string.Equals(roleContext.LocalSteamId, steamIds[index], StringComparison.Ordinal) &&"));
            Assert.That(localRule, Does.Contain("(localTransform != null && ReferenceEquals(panel.PlayerTransforms[index], localTransform)) ||"));
            Assert.That(localRule, Does.Contain("(localPlayerKnown && panel.PlayerIDs[index].Equals(localPlayerId))"));
            Assert.That(loop, Does.Contain("SteamIdTruthPolicy.Classify("));
            Assert.That(loop, Does.Contain("SteamIdTruthPolicy.ClassifyFromHostAttestation("));
        });
    }

    [Test]
    public void Systemic_transport_failure_disables_transport_truth_instead_of_locking_out_every_player()
    {
        string capture = ReadSource("PlayerIdentityEvidenceController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(capture, Does.Contain("if (transportTruth && remoteRows > 0 && transportVouched == 0)"));
            Assert.That(capture, Does.Contain("snapshot.TransportUnavailable = true;"));
            Assert.That(capture, Does.Contain("\"; transport=\""));
        });
    }

    [Test]
    public void Error_snapshot_never_blanks_the_published_attestation_and_the_heartbeat_always_runs()
    {
        string evidence = ReadSource("PlayerIdentityEvidenceController.cs");
        string build = Slice(evidence, "internal static string BuildHostSteamIdTruthAttestation(", "\n    }\n");
        string publish = Slice(ReadSource("SteamIdModerationController.cs"), "internal static void PublishHostCapability()", "internal static void ClearHostCapability()");

        Assert.Multiple(() =>
        {
            Assert.That(build, Does.Contain("return null;"));
            Assert.That(publish, Does.Contain("if (attestation != null)"));
            Assert.That(
                publish.IndexOf("Patches.PlayerRoleLabelController.PublishHostHeartbeat();", System.StringComparison.Ordinal),
                Is.LessThan(publish.IndexOf("BuildHostSteamIdTruthAttestation(", System.StringComparison.Ordinal)));
            Assert.That(publish, Does.Contain("SteamID truth attestation publish skipped"));
        });
    }

    [Test]
    public void Verified_authority_roster_requires_trusted_truth_and_Steam_lobby_membership()
    {
        string loop = Slice(ReadSource("PlayerIdentityEvidenceController.cs"), "var result = new List<PlayerIdentityEvidence>();", "snapshot.Verified = result");

        Assert.Multiple(() =>
        {
            Assert.That(loop, Does.Contain("bool confirmedRow = exactLiveRow && SteamIdTruthPolicy.IsTrusted(truth);"));
            Assert.That(loop, Does.Contain("bool verifiedRow = confirmedRow && currentSteamMember;"));
        });
    }

    [Test]
    public void Id_cards_use_the_confirmed_roster_while_authority_callers_keep_Verified()
    {
        string controller = ReadSource("NativeIdCardController.cs");
        string evidence = ReadSource("PlayerIdentityEvidenceController.cs");
        string strict = Slice(evidence, "internal static bool TryResolveExact(", "internal static bool TryResolveConfirmed(");
        string confirmed = Slice(evidence, "internal static bool TryResolveConfirmed(", "internal static bool TryResolveExactWithAuthorityFingerprint(");

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("PlayerIdentityEvidenceController.TryResolveConfirmed("));
            Assert.That(controller, Does.Not.Contain("PlayerIdentityEvidenceController.TryResolveExact("));
            Assert.That(strict, Does.Contain("snapshot.Verified"));
            Assert.That(confirmed, Does.Contain("snapshot.IdentityConfirmed"));
        });
    }

    [Test]
    public void Only_a_ready_Helper_consumes_the_host_attestation()
    {
        string evidence = ReadSource("PlayerIdentityEvidenceController.cs");
        string read = Slice(evidence, "private static Dictionary<string, SteamIdTruthAttestationEntry> ReadHelperAttestation(", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(read, Does.Contain("LobbySafetyAccessState.HelperReady"));
            Assert.That(read, Does.Contain("SteamIdTruthAttestationPolicy.TryParse("));
            Assert.That(read, Does.Contain("SteamMatchmaking.GetLobbyOwner("));
        });
    }

    [Test]
    public void Host_publishes_and_clears_the_attestation_with_its_capability()
    {
        string moderation = ReadSource("SteamIdModerationController.cs");
        string publish = Slice(moderation, "internal static void PublishHostCapability()", "internal static void ClearHostCapability()");
        string clear = Slice(moderation, "internal static void ClearHostCapability()", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(publish, Does.Contain("SteamIdTruthKey"));
            Assert.That(publish, Does.Contain("PlayerIdentityEvidenceController.BuildHostSteamIdTruthAttestation("));
            Assert.That(clear, Does.Contain("SetLobbyData(lobby, SteamIdTruthKey, string.Empty)"));
        });
    }

    [Test]
    public void Coverage_line_reports_truth_counts_and_lobby_member_limit_without_SIDs()
    {
        string evidence = ReadSource("PlayerIdentityEvidenceController.cs");
        string log = Slice(evidence, "private static void LogCoverageTransition(", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(log, Does.Contain("\"; confirmed=\""));
            Assert.That(log, Does.Contain("\"; transportVerified=\""));
            Assert.That(log, Does.Contain("\"; hostAttested=\""));
            Assert.That(log, Does.Contain("\"; steamIdMismatch=\""));
            Assert.That(log, Does.Contain("\"; lobbyLimit=\""));
            Assert.That(log, Does.Not.Contain("SteamId +"));
        });
    }

    [Test]
    public void Refused_ID_card_click_tells_the_player_why_in_plain_words()
    {
        string controller = ReadSource("NativeIdCardController.cs");

        // Round 5h: chat twin as well, so the message survives Toast Pop-ups being off.
        Assert.That(controller, Does.Contain("Plugin.AddLocalNotificationWithToast(NativeIdCardUnconfirmedToast)"));
    }

    private static string ReadSource(string fileName)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", fileName)).Replace("\r\n", "\n");
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }
}
