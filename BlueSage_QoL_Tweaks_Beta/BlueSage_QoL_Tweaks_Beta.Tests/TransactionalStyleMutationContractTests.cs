using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class TransactionalStyleMutationContractTests
{
    [Test]
    public void Plugin_validates_and_checks_player_readiness_before_native_or_config_commit()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        int transaction = source.IndexOf("private BlueSageCommandResult TryApplyDisplayNameDraft", StringComparison.Ordinal);
        int validation = source.IndexOf("DisplayNameMutationPolicy.TryCompose", transaction, StringComparison.Ordinal);
        int readiness = source.IndexOf("dataManager == null || dataManager.PlayerData == null", transaction, StringComparison.Ordinal);
        int nativeSyncReadiness = source.IndexOf("textChannelManager == null || textChannelManager.MainCustomizationController == null", transaction, StringComparison.Ordinal);
        int nativeMutation = source.IndexOf("dataManager.PlayerData.Name = displayName", transaction, StringComparison.Ordinal);
        int serializedReadback = source.IndexOf("Encoding.Unicode.GetString(playerInfo.Name", transaction, StringComparison.Ordinal);
        int configCommit = source.IndexOf("CommitDisplayNameDraft(draft);", transaction, StringComparison.Ordinal);

        Assert.That(transaction, Is.GreaterThanOrEqualTo(0));
        Assert.That(validation, Is.GreaterThan(transaction));
        Assert.That(readiness, Is.GreaterThan(validation));
        Assert.That(nativeSyncReadiness, Is.GreaterThan(readiness));
        Assert.That(nativeMutation, Is.GreaterThan(nativeSyncReadiness));
        Assert.That(serializedReadback, Is.GreaterThan(nativeMutation));
        Assert.That(configCommit, Is.GreaterThan(serializedReadback));
        Assert.That(source, Does.Contain("RestoreDisplayNameConfig(previousConfig);"));
        Assert.That(source, Does.Contain("TryRestoreNativeDisplayName"));
        Assert.That(source, Does.Contain("PlayerIdInfoLimitPatch.HasExpectedCoverage"));
        Assert.That(source, Does.Contain("serializedName, displayName"));
        Assert.That(source, Does.Contain("\"Your player info is still loading. Try again in a moment.\""));
    }

    [Test]
    public void Public_style_commands_build_drafts_instead_of_persisting_before_apply()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Plugin.cs"));
        string commandRegion = Slice(source, "private BlueSageCommandResult HandleSetNameCommand", "private BlueSageCommandResult ApplyStatusNameToPlayerData");

        Assert.That(commandRegion, Does.Contain("TryApplyDisplayNameDraft"));
        Assert.That(commandRegion, Does.Not.Contain("StatusBaseName.Value ="));
        Assert.That(commandRegion, Does.Not.Contain("StatusMessage.Value ="));
        Assert.That(commandRegion, Does.Not.Contain("StatusColor.Value ="));
        Assert.That(commandRegion, Does.Not.Contain("EnableSpoons.Value ="));
        Assert.That(commandRegion, Does.Not.Contain("SpoonCount.Value ="));
        Assert.That(commandRegion, Does.Not.Contain("SpoonLabel.Value ="));
        Assert.That(commandRegion, Does.Not.Contain("Config.Save();"));
    }

    [Test]
    public void Style_helper_marks_failed_handler_actions_as_drafts()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(source, Does.Contain("MarkApplyOutcome(applied"));
        Assert.That(source, Does.Contain("TryApplyStyledNameFromStyleUi(_preview, out applied)"));
        Assert.That(source, Does.Contain("TryApplyStatusFromStyleUi(null, _status, CleanColor(_statusColor), out applied)"));
        Assert.That(source, Does.Contain("TryClearStatusFromStyleUi(out applied)"));
        Assert.That(source, Does.Contain("TryApplySpoonsFromStyleUi"));
        Assert.That(source, Does.Not.Contain("Plugin.SpoonCount.Value = clamped"));
        Assert.That(source, Does.Not.Contain("Plugin.EnableSpoons.Value = true"));
        Assert.That(source, Does.Not.Contain("Plugin.EnableSpoons.Value = false"));
    }

    private static string Slice(string source, string start, string end)
    {
        int from = source.IndexOf(start, StringComparison.Ordinal);
        int to = source.IndexOf(end, from, StringComparison.Ordinal);
        Assert.That(from, Is.GreaterThanOrEqualTo(0));
        Assert.That(to, Is.GreaterThan(from));
        return source.Substring(from, to - from);
    }

    private static string ProjectRoot
    {
        get
        {
            string current = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(current, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
            }

            return current;
        }
    }
}
