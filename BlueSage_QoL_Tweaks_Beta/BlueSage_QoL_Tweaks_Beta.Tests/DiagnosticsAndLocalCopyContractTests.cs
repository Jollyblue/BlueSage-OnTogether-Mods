using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class DiagnosticsAndLocalCopyContractTests
{
    private static readonly string Root = FindProjectRoot();

    // Round 4 WS2 (Blue, 2026-10-19): the hidden /bluedevdebug developer diagnostics (and "/bsqol diag") are
    // removed. Players share logs through the menu's local audit snapshot and folder copy instead.
    [Test]
    public void Hidden_developer_diagnostics_are_removed_and_the_menu_audit_copy_remains()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string panel = File.ReadAllText(Path.Combine(Root, "src", "Patches", "PlayerPanelSurfaceController.cs"));
        string menu = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Not.Contain("HiddenDiagnostics"));
            Assert.That(plugin, Does.Not.Contain("[BlueSageDiag]"));
            Assert.That(plugin, Does.Not.Contain("DecodeCommandName(\"cmvfefwefcvh\")"));
            Assert.That(panel, Does.Not.Contain("EmitHierarchyDiagnosticIfEnabled"));
            Assert.That(plugin, Does.Contain("EnableLocalAuditExports = Config.Bind(\"Local Audit\""));
            Assert.That(menu, Does.Contain("DrawLocalAuditActions("));
        });
    }

    // Round 4 review R9 (2026-10-19): the /bluedevdebug removal left its feeders behind. Nothing read them any
    // more (grep: zero callers outside their own chains), and the WS2 receipt said the diagnostics were gone.
    // These negative pins keep them gone; the dead-code register has the restore commands.
    [Test]
    public void Diagnostics_orphans_from_the_bluedevdebug_removal_stay_removed()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string panel = File.ReadAllText(Path.Combine(Root, "src", "Patches", "PlayerPanelSurfaceController.cs"));
        string chat = File.ReadAllText(Path.Combine(Root, "src", "Patches", "ChatReadabilityPatch.cs"));
        string voice = File.ReadAllText(Path.Combine(Root, "src", "Patches", "VoicePositionRateGuardPatch.cs"));
        string identity = File.ReadAllText(Path.Combine(Root, "src", "PlayerIdentityEvidenceController.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Not.Contain("TryGetLobbySurfaceMetadata"));
            Assert.That(plugin, Does.Not.Contain("SanitizeDiagnosticValue"));
            Assert.That(plugin, Does.Not.Contain("ReadLobbyData("), "Only the lobby-surface chain used it.");
            Assert.That(plugin, Does.Not.Contain("TryReadStringProperty("));
            Assert.That(plugin, Does.Not.Contain("TryReadBoolProperty("));
            Assert.That(plugin, Does.Not.Contain("FirstUseful("));
            Assert.That(plugin, Does.Not.Contain("_lastHostHealthDecision"), "Write-only field.");
            Assert.That(chat, Does.Not.Contain("GetDiagnosticStateSummary"));
            Assert.That(voice, Does.Not.Contain("GetDiagnosticSummary"));
            Assert.That(voice, Does.Not.Contain("_forwarded"), "Counted only for the removed summary.");
            Assert.That(identity, Does.Not.Contain("GetDiagnosticRosterSummary"));
            Assert.That(identity, Does.Contain("FormatDiagnosticRosterSummary("), "Still used by the resync report.");
            Assert.That(panel, Does.Not.Contain("GetDiagnosticSummary"));
            Assert.That(panel, Does.Not.Contain("System.Threading.Interlocked"), "The lane/button counters fed only the summaries.");
            Assert.That(panel, Does.Not.Contain("SyncButtonField"));
            Assert.That(panel, Does.Not.Contain("SyncImageField"));
            Assert.That(panel, Does.Not.Contain("SteamButtonField"));
            Assert.That(panel, Does.Not.Contain("VoiceButtonField"));
            Assert.That(panel, Does.Contain("ReportButtonField"), "The one reflection handle the lane layout reads stays.");
            Assert.That(panel, Does.Contain("PlayerPanelSurfaceController.UnregisterOwnedLane(this);"), "OnDestroy still unregisters the lane.");
        });
    }

    // Round 4 review R7 (2026-10-19): with the command gone, a tester who still types /bluedevdebug (old UAT
    // docs told them to) broadcasts it to the lobby. A hidden tombstone answers locally and points at the menu's
    // audit snapshot; it never appears in suggestions.
    [Test]
    public void Typing_the_retired_bluedevdebug_command_answers_locally_and_is_not_suggested()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs")).Replace("\r\n", "\n");
        int registration = plugin.IndexOf("\"bluedevdebug\",", StringComparison.Ordinal);
        string block = registration < 0 ? string.Empty : plugin.Substring(registration, Math.Min(400, plugin.Length - registration));
        int handler = plugin.IndexOf("private BlueSageCommandResult HandleRetiredDeveloperDiagnosticsCommand(", StringComparison.Ordinal);
        string body = handler < 0 ? string.Empty : plugin.Substring(handler, plugin.IndexOf("\n    }\n", handler, StringComparison.Ordinal) - handler);

        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("bluedevdebug", "retired", Array.Empty<string>(), _ => BlueSageCommandResult.HandledWithMessages("retired"), isHidden: true));

        Assert.Multiple(() =>
        {
            Assert.That(registration, Is.GreaterThan(0), "The tombstone is registered.");
            Assert.That(block, Does.Contain("isHidden: true"));
            Assert.That(block, Does.Contain("HandleRetiredDeveloperDiagnosticsCommand"));
            Assert.That(body, Does.Contain("BlueSageCommandResult.HandledWithMessages("), "Handled locally: the text never reaches the lobby.");
            Assert.That(body, Does.Contain("removed"));
            Assert.That(body, Does.Contain("Local Audit"), "Points at the menu's audit snapshot and folder copy.");
            Assert.That(registry.TryExecute("/bluedevdebug off").Handled, Is.True);
            Assert.That(registry.Suggest("/blue", 5), Is.Empty, "Hidden commands are never suggested.");
            Assert.That(plugin, Does.Not.Contain("status, diag, hosthealth"), "/bsqol no longer advertises the removed diag sub-command.");
        });
    }

    [Test]
    public void Local_notifications_use_the_same_safe_copy_and_link_path_as_chat_rows()
    {
        string patch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "ChatCopyMessagePatch.cs"));

        Assert.That(patch, Does.Contain("[HarmonyPatch(\"AddNotification\", typeof(string))]"));
        Assert.That(patch, Does.Contain("TextContentGlobalTransform"));
        Assert.That(patch, Does.Contain("AttachToCreatedRow"));
        Assert.That(patch, Does.Contain("ChatRowReconciliationPolicy.Resolve("));
        Assert.That(patch, Does.Contain("ChatCopySanitizer.ToClipboardText(sourceText)"));
        Assert.That(patch, Does.Contain("ChatUrlDetector.TryGetFirstUrl"));
        Assert.That(patch, Does.Contain("handler.Initialize(payload, safeUrl, senderContext)"));
        Assert.That(patch, Does.Contain("text, null)"));
        Assert.That(patch, Does.Not.Contain("userName +"));
    }

    [Test]
    // Round 4 WS2: the forced /bluedevdebug snapshot and panel hierarchy trace are gone; the reflection
    // null-guard keeps its own bounded, de-duplicated reporting.
    public void Reflection_guard_reporting_stays_bounded_after_diagnostics_removal()
    {
        string reflectionPatch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "NetworkReflectionNullGuardPatch.cs"));

        Assert.That(reflectionPatch, Does.Contain("HashSet<string> ReportedSurfaces"));
        Assert.That(reflectionPatch, Does.Contain("MaximumDistinctDiagnostics = 32"));
        Assert.That(reflectionPatch, Does.Not.Contain("ReportedInstances"));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
