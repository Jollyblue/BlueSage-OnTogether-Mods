using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class DiagnosticsAndLocalCopyContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Diagnostics_are_explicit_opt_in_and_can_be_disabled_for_large_lobby_play()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));

        Assert.That(plugin, Does.Contain("ShouldEmitHiddenDiagnostics =>"));
        Assert.That(plugin, Does.Contain("EnableHiddenDiagnostics != null && EnableHiddenDiagnostics.Value"));
        Assert.That(plugin, Does.Contain("Config.Bind(\"Developer\", \"EnableHiddenDiagnostics\", false,"));
        Assert.That(plugin, Does.Not.Contain("EnforceHiddenDiagnosticsLockedOn"));
        Assert.That(plugin, Does.Contain("case \"off\":"));
        Assert.That(plugin, Does.Contain("EnableHiddenDiagnostics.Value = false;"));
        Assert.That(plugin, Does.Contain("BlueSage diagnostics disabled. Large-lobby play stays quieter"));
        Assert.That(plugin, Does.Contain("Shareable BepInEx diagnostics omit exact player/lobby identities"));
    }

    [Test]
    public void Startup_migration_never_reenables_opt_in_diagnostics()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        int enableAssignments = plugin.Split("EnableHiddenDiagnostics.Value = true;", StringSplitOptions.None).Length - 1;

        Assert.That(enableAssignments, Is.EqualTo(1),
            "Only the explicit /bluedevdebug on command may enable diagnostics; startup migration must preserve the saved off choice.");
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
    public void Forced_diagnostics_keep_actionable_evidence_without_repeating_large_lobby_payloads()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string panelController = File.ReadAllText(Path.Combine(Root, "src", "Patches", "PlayerPanelSurfaceController.cs"));
        string reflectionPatch = File.ReadAllText(Path.Combine(Root, "src", "Patches", "NetworkReflectionNullGuardPatch.cs"));

        Assert.That(plugin, Does.Contain("panelRosterFingerprint"));
        Assert.That(plugin, Does.Contain("identityDetail=private-audit-only"));
        Assert.That(plugin, Does.Contain("panelSteamIdList=omitted-public"));
        Assert.That(plugin, Does.Contain("BuildPlayerRosterFingerprint"));
        Assert.That(panelController, Does.Contain("HierarchyDiagnosticMaximumEntries = 128"));
        Assert.That(panelController, Does.Contain("HierarchyDiagnosticRepeatSeconds = 30f"));
        Assert.That(panelController, Does.Contain("HierarchyDiagnosticMaximumPathLength = 256"));
        Assert.That(panelController, Does.Contain("authority=diagnostic-only"));
        Assert.That(panelController, Does.Contain("fallbackEnabled=false"));
        Assert.That(panelController, Does.Contain("LastHierarchyDiagnosticAt.Count"));
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
