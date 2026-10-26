using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SessionAuditContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Local_audit_defaults_on_is_disclosed_and_has_an_explicit_opt_out()
    {
        string plugin = Read("src", "Plugin.cs");
        string menu = Read("src", "QolMenuWindow.cs");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Match(@"EnableLocalAuditExports\s*=\s*Config\.Bind\([^;]+,\s*false,"));
            Assert.That(plugin, Does.Contain("Local Audit is off unless you turn it on"));
            Assert.That(plugin, Does.Contain("\"auditlog\""));
            Assert.That(plugin, Does.Contain("/auditlog status, on, off, path, or snapshot"));
            Assert.That(plugin, Does.Contain("Your saved files stay on this computer"));
            Assert.That(menu, Does.Contain("DrawToggle(\"Local Audit Exports\""));
            Assert.That(menu, Does.Contain("SessionAuditController.ReconcileEnabledState"));
            Assert.That(menu, Does.Contain("DrawLocalAuditActions(showIdentityNote: false)"));
            Assert.That(menu, Does.Contain("Snapshot Now"));
            Assert.That(menu, Does.Contain("Copy Audit Folder"));
        });
    }

    [Test]
    public void Chat_and_game_log_exports_are_bounded_local_only_and_never_uploaded()
    {
        string controller = Read("src", "SessionAuditController.cs");
        string snapshotPolicy = Read("src", "Core", "AuditSnapshotPolicy.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("internal static string AuditRoot => ModSuitePaths.Current.Logs;"), "Round 5d: BlueSageQoLModSuite/logs.");
            Assert.That(controller, Does.Contain("Path.Combine(Paths.BepInExRootPath, \"LogOutput.log\")"));
            Assert.That(controller, Does.Contain("FileShare.ReadWrite | FileShare.Delete"));
            Assert.That(controller, Does.Contain("MaxChatPartBytes = LocalFileBudgetPolicy.AuditChatPartBytes"));
            Assert.That(controller, Does.Contain("MaxBepInExPartBytes = LocalFileBudgetPolicy.AuditMirrorPartBytes"));
            Assert.That(controller, Does.Contain("ModSuitePaths.RunRetentionInBackground(_sessionDirectory)"), "Older sessions are zipped into the archive by the shared retention pass.");
            Assert.That(controller, Does.Contain("never uploaded automatically"));
            Assert.That(controller, Does.Contain("MarkCaptureFailed"));
            // Voice pass (2026-10-23): one calm Local Audit voice; failures still answer, without shouting.
            Assert.That(controller, Does.Contain("internal const string OffText = \"Local Audit is off (that's the default). Type /auditlog on if you'd like to keep local log copies.\";"));
            Assert.That(controller, Does.Contain("internal const string WriteTroubleText = \"Local Audit is on but couldn't save files just now. Try /auditlog on again in a moment.\";"));
            Assert.That(controller, Does.Contain("internal const string SnapshotFailedText = \"The snapshot couldn't be saved this time. Nothing was lost; try again in a moment.\";"));
            Assert.That(controller, Does.Contain("internal const string ResumedText = \"Local Audit is back on. Anything from while it was off wasn't saved.\";"));
            Assert.That(controller, Does.Contain("\"Local Audit is on. Your files are in \" + CurrentSessionDirectory + \". A snapshot saves the newest 8 MB of your game log as one file; the 2 newest are kept for each session.\""));
            Assert.That(controller, Does.Contain("return \"Snapshot saved: \" + snapshotPath + \". Its file path is copied, ready to paste.\";"));
            Assert.That(controller, Does.Not.Contain("FAILED"));
            Assert.That(controller, Does.Not.Contain("NOT WRITING"));
            Assert.That(controller, Does.Not.Contain("HttpClient"));
            Assert.That(controller, Does.Not.Contain("WebClient"));
            Assert.That(controller, Does.Not.Contain("WebRequest"));
            Assert.That(controller, Does.Not.Contain("Upload"));
            Assert.That(controller, Does.Contain("AuditSnapshotPolicy.TryCreate"));
            Assert.That(controller, Does.Contain("bytes from the true BepInEx LogOutput.log"), "The byte count and source stay in the log.");
            Assert.That(snapshotPolicy, Does.Contain("temporaryPath = finalPath + \".tmp\";"));
            Assert.That(snapshotPolicy, Does.Contain("LocalFileBudgetPolicy.AuditSnapshotMaxBytes"));
            Assert.That(snapshotPolicy, Does.Contain("FileShare.ReadWrite | FileShare.Delete"));
            Assert.That(snapshotPolicy, Does.Contain("MaxRetainedSnapshots = LocalFileBudgetPolicy.AuditSnapshotsPerSession"));
        });
    }

    [Test]
    public void Chat_capture_precedes_copy_toggle_and_preserves_raw_and_plain_evidence()
    {
        string patch = Read("src", "Patches", "ChatCopyMessagePatch.cs");
        string policy = Read("src", "Core", "SessionAuditPolicy.cs");
        int capture = patch.IndexOf("SessionAuditController.RecordChat", StringComparison.Ordinal);
        int copyGate = patch.IndexOf("ShouldApplyChatCopy", StringComparison.Ordinal);
        Assert.Multiple(() =>
        {
            Assert.That(capture, Is.GreaterThanOrEqualTo(0));
            Assert.That(copyGate, Is.GreaterThan(capture));
            Assert.That(policy, Does.Contain("displayNameRaw"));
            Assert.That(policy, Does.Contain("displayNamePlain"));
            Assert.That(policy, Does.Contain("rosterNameRaw"));
            Assert.That(policy, Does.Contain("steamPersona"));
            Assert.That(policy, Does.Contain("messageRaw"));
            Assert.That(policy, Does.Contain("messagePlain"));
            Assert.That(policy, Does.Contain("MaximumAuditFieldCharacters = 8192"));
        });
    }

    [Test]
    public void Exact_identity_enrichment_is_access_scoped_and_native_bans_are_observed()
    {
        string controller = Read("src", "SessionAuditController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("Plugin.CanUseLobbySafety(out bool isHost)"));
            Assert.That(controller, Does.Contain("redacted-normal-client"));
            Assert.That(controller, Does.Contain("AuthoritativeChatSenderContext senderContext"));
            Assert.That(controller, Does.Contain("senderContext?.ExactTupleCaptured"));
            Assert.That(controller, Does.Not.Contain("authorized && senderIndex > 0"));
            Assert.That(controller, Does.Contain("PollNativeBanStore"));
            Assert.That(controller, Does.Contain("native-ban-store-observed"));
            Assert.That(controller, Does.Contain("SteamIdModerationController.TryGetNativeBanRecords"));
            Assert.That(controller, Does.Contain("CapturePersistedNativeBanSave"));
            Assert.That(controller, Does.Contain("issuerSteamId"));
            Assert.That(controller, Does.Contain("incidentId"));
        });
    }

    [Test]
    public void Player_lookup_is_match_all_but_helper_assignment_requires_one_verified_result()
    {
        string plugin = Read("src", "Plugin.cs");
        string evidence = Read("src", "PlayerIdentityEvidenceController.cs");
        string policy = Read("src", "Core", "PlayerLookupPolicy.cs");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("ResolveLookupMatches(lookup"));
            Assert.That(plugin, Does.Contain("bool copyEvidence = arguments.StartsWith(\"copy \""));
            Assert.That(plugin, Does.Contain("matches.Count > 1"));
            Assert.That(plugin, Does.Contain("TryResolveLobbyMemberSteamId(tokens[1]"));
            Assert.That(evidence, Does.Contain("TryResolveLookup"));
            Assert.That(evidence, Does.Contain("matches.Count == 1"));
            Assert.That(evidence, Does.Contain("matches.Count > 1"));
            Assert.That(policy, Does.Contain("TimeSpan.FromMilliseconds(30)"));
            Assert.That(policy, Does.Contain("MaximumQueryCharacters = 96"));
            Assert.That(policy, Does.Contain("MinimumPartialSteamIdDigits = 4"));
            Assert.That(plugin, Does.Not.Contain("\"awi\""));
            Assert.That(plugin, Does.Not.Contain("\"alphawhois\""));
        });
    }

    [Test]
    public void Helper_exact_identity_relays_use_non_chat_member_data()
    {
        string moderation = Read("src", "SteamIdModerationController.cs");
        string incidents = Read("src", "CloneIncidentActionController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(moderation, Does.Contain("ModerationProtocolPolicy.MemberDataKey"));
            Assert.That(moderation, Does.Contain("SteamMatchmaking.SetLobbyMemberData"));
            Assert.That(moderation, Does.Not.Contain("Plugin.TrySendLobbyChatMessage(request)"));
            Assert.That(incidents, Does.Contain("CloneIncidentResolutionProtocolPolicy.MemberDataKey"));
            Assert.That(incidents, Does.Contain("SteamMatchmaking.SetLobbyMemberData"));
            Assert.That(incidents, Does.Not.Contain("Plugin.TrySendLobbyChatMessage(packet)"));
        });
    }

    [Test]
    public void Row_zero_is_enriched_only_from_the_exact_captured_rpc_tuple()
    {
        string audit = Read("src", "SessionAuditController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(audit, Does.Contain("senderContext?.ExactTupleCaptured == true"));
            Assert.That(audit, Does.Contain("auditedSenderIndex = senderContext.NativeRowIndex"));
            Assert.That(audit, Does.Not.Contain("authorized && senderIndex > 0"));
            Assert.That(audit, Does.Not.Contain("unverified-ambiguous-sender-index-zero"));
        });
    }

    [Test]
    public void SaveBanData_postfix_captures_every_successful_native_persistence_sink()
    {
        string patch = Read("src", "Patches", "NativeBanAuditPatch.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("DataManager.SaveBanData"));
            Assert.That(patch, Does.Contain("CapturePersistedNativeBanSave"));
            Assert.That(patch, Does.Contain("HarmonyFinalizer"));
            Assert.That(moderation, Does.Contain("DisplayNameRaw"));
            Assert.That(moderation, Does.Contain("ArmPersistedModerationAudit"));
        });
    }

    [Test]
    public void Moderation_confirmation_must_repeat_the_previewed_target()
    {
        string plugin = Read("src", "Plugin.cs");
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("bare confirm is refused"));
            Assert.That(plugin, Does.Contain("confirmation must repeat the previewed incident ID or full SteamID64"));
            Assert.That(plugin, Does.Contain("confirmation does not match the previewed target"));
        });
    }

    [Test]
    public void Helper_local_incident_numbers_are_never_presented_as_host_local_evidence()
    {
        string moderation = Read("src", "SteamIdModerationController.cs");
        Assert.That(moderation, Does.Contain("issuer-local:"));
        Assert.That(moderation, Does.Contain("!string.Equals(issuerSteamId, ownerSteamId"));
    }

    private static string Read(params string[] path) => File.ReadAllText(Path.Combine(new[] { Root }.Concat(path).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
