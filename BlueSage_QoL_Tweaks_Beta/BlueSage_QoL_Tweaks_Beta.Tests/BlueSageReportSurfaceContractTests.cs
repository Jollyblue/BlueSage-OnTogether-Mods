using BlueSage.QoLTweaks.Core;
using NUnit.Framework;
using System.IO;

namespace BlueSage.QoLTweaks.Tests;

[TestFixture]
internal sealed class BlueSageReportSurfaceContractTests
{
    private static string ReadRuntimeSource()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
        return File.ReadAllText(Path.Combine(root, "src", "BlueSageReportWindow.cs"));
    }

    [Test]
    public void Report_open_draws_first_and_batches_native_achievement_refresh_across_frames()
    {
        string source = ReadRuntimeSource();
        int openStart = source.IndexOf("internal void Open()", StringComparison.Ordinal);
        int closeStart = source.IndexOf("internal void CloseReport()", openStart, StringComparison.Ordinal);
        string openBody = source.Substring(openStart, closeStart - openStart);

        Assert.Multiple(() =>
        {
            Assert.That(openBody, Does.Contain("RequestRefresh()"));
            Assert.That(openBody, Does.Not.Contain("\n        Refresh();"));
            Assert.That(source, Does.Contain("private IEnumerator RefreshRoutine()"));
            Assert.That(source, Does.Contain("yield return null;"));
            Assert.That(source, Does.Contain("ReadNextAchievementBatch(AchievementBatchSize)"));
            Assert.That(source, Does.Contain("BlueSage Report refresh trace:"));
        });
    }

    [Test]
    public void Journal_faces_reuse_native_setfeel_button_sprites_with_text_fallback()
    {
        string source = ReadRuntimeSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("BlueSageJournalMoodTextureResolver"));
            Assert.That(source, Does.Contain("_moodIcons"));
            Assert.That(source, Does.Contain("DrawTextureWithTexCoords"));
            Assert.That(source, Does.Contain("Native Journal face textures unavailable; plain-English labels remain active."));
            Assert.That(source, Does.Contain("BlueSageJournalMoodPresentation.Label(feel)"));
        });
    }

    [Test]
    public void Exact_anchor_gate_fails_open_and_logs_only_once()
    {
        var gate = new BlueSageReportAnchorGate();
        int logs = 0;

        Assert.That(gate.TryEnable(new BlueSageReportAnchors(true, false, true, true, true, true), () => logs++), Is.False);
        Assert.That(gate.TryEnable(new BlueSageReportAnchors(true, false, true, true, true, true), () => logs++), Is.False);
        Assert.That(logs, Is.EqualTo(1));
        Assert.That(gate.NativeReportEnabled, Is.True);
        Assert.That(gate.BlueSagePanelOwnedSeparately, Is.False);
    }

    [Test]
    public void Sibling_panel_never_takes_over_native_report_after_close_or_reopen()
    {
        bool native = true, blueSage = false, window = false;
        var controller = new BlueSageReportTabController(
            value => native = value, value => blueSage = value,
            () => window = true, () => window = false);

        controller.SelectBlueSage();
        Assert.That((native, blueSage, window), Is.EqualTo((false, true, true)));
        controller.Close();
        Assert.That((native, blueSage, window), Is.EqualTo((true, false, false)));
        controller.SelectBlueSage();
        controller.Reopen();
        Assert.That((native, blueSage, window), Is.EqualTo((true, false, false)));
        Assert.That(controller.TryClaimAttachment(42), Is.True);
        Assert.That(controller.TryClaimAttachment(42), Is.False);
    }


    [Test]
    public void Anchor_gate_requires_lifecycle_button_and_writable_label()
    {
        var gate = new BlueSageReportAnchorGate();
        Assert.That(gate.TryEnable(new BlueSageReportAnchors(true, true, true, false, true, true), () => { }), Is.False);
        Assert.That(gate.TryEnable(new BlueSageReportAnchors(true, true, true, true, false, true), () => { }), Is.False);
        Assert.That(gate.TryEnable(new BlueSageReportAnchors(true, true, true, true, true, false), () => { }), Is.False);
        Assert.That(BlueSageReportSurfacePolicy.FailOpenDiagnostic,
            Is.EqualTo("BlueSage Report disabled: exact native Report anchors are incomplete; native Report remains unchanged."));
    }

    [Test]
    public void Controller_registry_prunes_dead_surfaces_and_restore_failures_cannot_escape()
    {
        bool alive = true;
        var registry = new BlueSageReportControllerRegistry();
        var first = new BlueSageReportTabController(_ => throw new InvalidOperationException("destroyed"),
            _ => { }, () => { }, () => { }, () => alive);
        registry.Register(7, first);
        Assert.That(registry.RestoreAll(), Is.EqualTo(0));
        alive = false;
        registry.Prune();
        Assert.That(registry.Count, Is.Zero);

        bool native = false;
        registry.Register(7, new BlueSageReportTabController(value => native = value,
            _ => { }, () => { }, () => { }, () => true));
        Assert.That(registry.Count, Is.EqualTo(1));
        Assert.That(registry.RestoreAll(), Is.EqualTo(1));
        Assert.That(native, Is.True);
        Assert.That(BlueSageReportSurfacePolicy.LifecycleFailOpenDiagnostic,
            Is.EqualTo("BlueSage Report disabled: OpenStatisticsPanel lifecycle target is unavailable; native Report remains unchanged."));
    }

    [Test]
    public void Report_layout_is_fixed_and_event_ids_require_expanded_details()
    {
        Assert.That(BlueSageReportSurfacePolicy.FilterOrder,
            Is.EqualTo(new[] { "ALL", "WORK", "RELAX", "UNCLASSIFIED" }));
        Assert.That(BlueSageReportSurfacePolicy.SectionOrder,
            Is.EqualTo(new[] { "Overview", "Activity Snapshot", "Detailed Analysis", "Journal Mood Check-ins", "Achievements", "Classification History" }));

        var item = new BlueSageReportHistoryItem("event-secret", "5 minutes - Work", 1L);
        Assert.That(item.Render(false), Does.Not.Contain("event-secret"));
        Assert.That(item.Render(true), Does.Contain("event-secret"));
        Assert.That(BlueSageReportSurfacePolicy.QolFallbackLabel, Is.EqualTo("Open BlueSage Report"));
    }

    [Test]
    public void Journal_observation_boundary_allows_only_feel_and_time()
    {
        Assert.That(BlueSageReportPrivacyPolicy.IsAllowedJournalMember("Feel"), Is.True);
        Assert.That(BlueSageReportPrivacyPolicy.IsAllowedJournalMember("Time"), Is.True);
        Assert.That(BlueSageReportPrivacyPolicy.IsAllowedJournalMember("Headline"), Is.False);
        Assert.That(BlueSageReportPrivacyPolicy.IsAllowedJournalMember("Content"), Is.False);
        Assert.That(BlueSageReportPrivacyPolicy.IsAllowedJournalMember("ContentPath"), Is.False);
    }

    [TestCase("No eligible Unclassified mood observations are available for that amount.")]
    [TestCase("Requested amount exceeds the available Unclassified mood observations.")]
    public void Projection_refresh_keeps_mood_allocation_refusal_visible(string refusal)
    {
        var presenter = new BlueSageReportStatusPresenter();
        presenter.ShowActionResult(refusal);

        presenter.CompleteProjectionRefresh(true,
            "Native mood history is temporarily unavailable.", string.Empty);

        Assert.That(presenter.Text, Does.StartWith(refusal));
        Assert.That(presenter.Text, Does.Contain("Native mood history is temporarily unavailable."));
    }

    [Test]
    public void Projection_refresh_keeps_successful_correction_confirmation_visible()
    {
        var presenter = new BlueSageReportStatusPresenter();
        presenter.ShowActionResult("Classification updated.");

        presenter.CompleteProjectionRefresh(true, string.Empty, string.Empty);

        Assert.That(presenter.Text, Is.EqualTo("Classification updated."));
    }
}
