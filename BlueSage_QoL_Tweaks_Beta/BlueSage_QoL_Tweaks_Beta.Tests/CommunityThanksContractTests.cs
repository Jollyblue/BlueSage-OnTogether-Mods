using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CommunityThanksContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Main_routes_to_credits_without_rendering_supporter_entries()
    {
        string source = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));
        string main = Slice(source, "private void DrawCommunityThanks()", "private void OpenCommunitySupporters()");

        Assert.Multiple(() =>
        {
            Assert.That(main, Does.Contain("♥ COMMUNITY SUPPORTERS ♥"));
            Assert.That(main, Does.Contain("15 community supporters").And.Contain("16 public thank-you messages"));
            Assert.That(main, Does.Contain("OpenCommunitySupporters"));
            Assert.That(main, Does.Not.Contain("SupporterLedgerData.Transactions"));
            Assert.That(main, Does.Not.Contain("DrawSupporterDirectory"));
            Assert.That(main, Does.Not.Contain("DrawSelectedSupporterTransactions"));
            Assert.That(main, Does.Not.Contain("17 gifts").And.Not.Contain("_activeButtonStyle"));
        });
    }

    [Test]
    public void Supporters_live_only_in_credits_other_and_every_selection_keeps_that_tab()
    {
        string source = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));
        string customize = Slice(source, "private void DrawCustomizeTab()", "private void DrawCreditsOtherTab()");
        string credits = Slice(source, "private void DrawCreditsOtherTab()", "private void DrawAboutAndCredits()");
        string selection = Slice(source, "private void SelectSupporterInCredits", "private void DrawSelectedSupporterMessages");
        string selectedMessages = Slice(source, "private void DrawSelectedSupporterMessages", "private void DrawCommunityCrew");
        string ledger = File.ReadAllText(Path.Combine(Root, "src", "Data", "SupporterLedger.g.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(customize, Does.Not.Contain("Supporter"));
            Assert.That(customize, Does.Not.Contain("DrawCommunityCrew"));
            Assert.That(credits, Does.Contain("DrawSupporterCredits"));
            Assert.That(credits, Does.Contain("COMMUNITY SUPPORTERS"));
            Assert.That(credits, Does.Contain("DrawCommunityCrew"));
            Assert.That(selection, Does.Contain("_tab = 3;"));
            Assert.That(selection, Does.Contain("_selectedSupporterIndex == index ? -1 : index"));
            Assert.That(selectedMessages, Does.Contain("GetPublicMessages"));
            Assert.That(selectedMessages, Does.Contain("FormatDateLabel"));
            Assert.That(selectedMessages, Does.Not.Contain("BuildTransactionHeading").And.Not.Contain("GetDisplayMessage"));
            Assert.That(credits, Does.Not.Contain("GetTotalReceived").And.Not.Contain(" received"));
            Assert.That(source, Does.Not.Contain("Lycoris"));
            Assert.That(ledger, Does.Contain("Lexiaphantasia"));
        });
    }

    [Test]
    public void Both_public_windows_use_the_shared_coordinator_and_help_frame()
    {
        string qol = File.ReadAllText(Path.Combine(Root, "src", "QolMenuWindow.cs"));
        string styler = File.ReadAllText(Path.Combine(Root, "src", "StyleHelperWindow.cs"));

        foreach (string source in new[] { qol, styler })
        {
            Assert.That(source, Does.Contain("BlueSageWindowCoordinator"));
            Assert.That(source, Does.Contain("BlueSageHelpFrame"));
        }
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Start marker missing: " + startMarker);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
