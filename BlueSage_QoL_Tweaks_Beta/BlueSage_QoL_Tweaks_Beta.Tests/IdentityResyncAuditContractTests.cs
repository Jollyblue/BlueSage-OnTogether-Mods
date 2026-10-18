using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class IdentityResyncAuditContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Status_and_audit_are_bounded_to_counts_reasons_durations_and_lock_state()
    {
        string controller = Read("src", "IdentityResyncController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("[BlueSageIdentityResync]"));
            Assert.That(controller, Does.Contain("targetedRows="));
            Assert.That(controller, Does.Contain("healedRows="));
            Assert.That(controller, Does.Contain("durationMs="));
            Assert.That(controller, Does.Contain("lockedRows="));
            Assert.That(controller, Does.Contain("lastTransaction="));
            Assert.That(controller, Does.Not.Contain("SteamID64:"));
            Assert.That(controller, Does.Not.Contain("DisplayName"));
            Assert.That(controller, Does.Not.Contain("SteamPersona"));
            Assert.That(controller, Does.Not.Contain("NetworkPlayerId"));
        });
    }

    [Test]
    public void Session_audit_receives_only_the_bounded_identity_resync_receipt()
    {
        string controller = Read("src", "IdentityResyncController.cs");
        string audit = Read("src", "SessionAuditController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("SessionAuditController.RecordIdentityResync"));
            Assert.That(audit, Does.Contain("RecordIdentityResync("));
            Assert.That(audit, Does.Contain("targetedRows"));
            Assert.That(audit, Does.Contain("healedRows"));
            Assert.That(audit, Does.Contain("lockedRows"));
            Assert.That(audit, Does.Contain("durationMilliseconds"));
        });
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(path).ToArray()));

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
