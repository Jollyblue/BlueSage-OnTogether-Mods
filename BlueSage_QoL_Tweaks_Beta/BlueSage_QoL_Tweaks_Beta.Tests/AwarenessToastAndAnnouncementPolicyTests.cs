using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// UAT F3FAB760BEB5 (Blue, 2026-10-17): more toasts so laptop players with small screens can keep up
// with events, and a Host/Helper announcement toast for QoL users (restarts, lobby breaks).
public sealed class AwarenessToastAndAnnouncementPolicyTests
{
    [TestCase(1920f, 360f)]
    [TestCase(1280f, 360f)]
    [TestCase(360f, 328f)]
    [TestCase(200f, 168f)]
    public void Toast_width_never_runs_off_a_narrow_screen(float screenWidth, float expected)
    {
        Assert.That(ToastPolicy.ComputeWidth(screenWidth), Is.EqualTo(expected).Within(0.01f));
    }

    [Test]
    public void Identical_toast_still_on_screen_is_not_stacked_again()
    {
        var visible = new List<ToastEntry> { new ToastEntry("Cleanup finished.", 10f) };

        Assert.Multiple(() =>
        {
            Assert.That(ToastPolicy.IsDuplicateOfVisible("Cleanup finished.", visible, 12f), Is.True);
            Assert.That(ToastPolicy.IsDuplicateOfVisible("Cleanup finished.", visible, 10f + ToastPolicy.TotalLifetimeSeconds), Is.False);
            Assert.That(ToastPolicy.IsDuplicateOfVisible("Something else.", visible, 12f), Is.False);
        });
    }

    [Test]
    public void Announcement_text_is_trimmed_bounded_and_stripped_of_markup_and_separators()
    {
        string raw = "  <color=red>Restart</color> in 5 | grab drinks\n\tsoon  " + new string('x', 400);

        string clean = LobbyAnnouncementPolicy.Sanitize(raw);

        Assert.Multiple(() =>
        {
            Assert.That(clean, Does.StartWith("color=redRestart/color in 5 grab drinks soon"));
            Assert.That(clean, Does.Not.Contain("<").And.Not.Contain(">").And.Not.Contain("|").And.Not.Contain("\n"));
            Assert.That(clean.Length, Is.LessThanOrEqualTo(LobbyAnnouncementPolicy.MaxTextLength));
        });
    }

    [Test]
    public void Fresh_announcement_round_trips()
    {
        string value = LobbyAnnouncementPolicy.Build(1_800_000_000L, "a1b2c3d4", "Restarting in 5 minutes");

        bool parsed = LobbyAnnouncementPolicy.TryParse(value, 1_800_000_030L, out LobbyAnnouncement announcement);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(announcement.Nonce, Is.EqualTo("a1b2c3d4"));
            Assert.That(announcement.Text, Is.EqualTo("Restarting in 5 minutes"));
        });
    }

    [TestCase(1_800_000_000L + 121L, TestName = "Old_announcement_is_not_replayed_to_late_joiners")]
    [TestCase(1_800_000_000L - 6L, TestName = "Future_dated_announcement_beyond_clock_skew_is_rejected")]
    public void Announcement_outside_its_window_is_ignored(long now)
    {
        string value = LobbyAnnouncementPolicy.Build(1_800_000_000L, "a1b2c3d4", "Break time");

        Assert.That(LobbyAnnouncementPolicy.TryParse(value, now, out _), Is.False);
    }

    [TestCase("")]
    [TestCase("announce0|1800000000|a1b2c3d4|hi")]
    [TestCase("announce1|1800000000|short|hi")]
    [TestCase("announce1|1800000000|a1b2c3d4|")]
    [TestCase("announce1|1800000000|a1b2c3d4|<b>hi</b>")]
    public void Malformed_announcement_is_ignored(string value)
    {
        Assert.That(LobbyAnnouncementPolicy.TryParse(value, 1_800_000_000L, out _), Is.False);
    }

    [Test]
    public void Senders_wait_between_announcements_so_toasts_cannot_be_spammed()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LobbyAnnouncementPolicy.CanSend(lastSentUnixSeconds: 0L, nowUnixSeconds: 1_800_000_000L), Is.True);
            Assert.That(LobbyAnnouncementPolicy.CanSend(1_800_000_000L, 1_800_000_000L + LobbyAnnouncementPolicy.MinSecondsBetweenSends - 1), Is.False);
            Assert.That(LobbyAnnouncementPolicy.CanSend(1_800_000_000L, 1_800_000_000L + LobbyAnnouncementPolicy.MinSecondsBetweenSends), Is.True);
        });
    }

    [Test]
    public void Receivers_throttle_each_sender_so_a_modified_client_cannot_flood_chat()
    {
        // Review 2026-10-17: the send-side limit only binds honest senders.
        Assert.Multiple(() =>
        {
            Assert.That(LobbyAnnouncementPolicy.ShouldAcceptFromSender(lastAcceptedUnixSeconds: 0L, nowUnixSeconds: 1_800_000_000L), Is.True);
            Assert.That(LobbyAnnouncementPolicy.ShouldAcceptFromSender(1_800_000_000L, 1_800_000_000L + LobbyAnnouncementPolicy.MinSecondsBetweenReceives - 1), Is.False);
            Assert.That(LobbyAnnouncementPolicy.ShouldAcceptFromSender(1_800_000_000L, 1_800_000_000L + LobbyAnnouncementPolicy.MinSecondsBetweenReceives), Is.True);
        });
    }

    [TestCase(1_800_000_120L, true, TestName = "Announcement_exactly_120_seconds_old_still_shows")]
    [TestCase(1_799_999_995L, true, TestName = "Announcement_exactly_at_the_5_second_skew_edge_still_shows")]
    public void Announcement_window_edges_are_inclusive(long now, bool expected)
    {
        string value = LobbyAnnouncementPolicy.Build(1_800_000_000L, "a1b2c3d4", "Break time");

        Assert.That(LobbyAnnouncementPolicy.TryParse(value, now, out _), Is.EqualTo(expected));
    }

    [TestCase("a1b2c3d4", true)]
    [TestCase("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6", true)]
    [TestCase("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e", false)]
    public void Nonce_length_edges_are_enforced(string nonce, bool expected)
    {
        string value = "announce1|1800000000|" + nonce + "|hi";

        Assert.That(LobbyAnnouncementPolicy.TryParse(value, 1_800_000_000L, out _), Is.EqualTo(expected));
    }

    [Test]
    public void Full_length_announcement_round_trips_unchanged()
    {
        string text = new string('y', LobbyAnnouncementPolicy.MaxTextLength);

        bool parsed = LobbyAnnouncementPolicy.TryParse(LobbyAnnouncementPolicy.Build(1_800_000_000L, "a1b2c3d4", text + "zzz"), 1_800_000_000L, out LobbyAnnouncement announcement);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(announcement.Text, Is.EqualTo(text));
        });
    }

    [Test]
    public void Ordinary_toasts_never_push_out_a_pinned_host_announcement()
    {
        var toasts = new List<ToastEntry>
        {
            new ToastEntry("Host announcement: Restart soon", 1f, pinned: true),
            new ToastEntry("Toast Pop-ups: On", 2f),
            new ToastEntry("Spoons: On", 3f),
            new ToastEntry("Timestamps: On", 4f),
            new ToastEntry("Chat Backdrop: On", 5f),
        };

        Assert.That(ToastPolicy.IndexToEvict(toasts), Is.EqualTo(1));
    }

    [Test]
    public void When_every_toast_is_pinned_the_oldest_goes_first()
    {
        var toasts = new List<ToastEntry>
        {
            new ToastEntry("a", 1f, pinned: true),
            new ToastEntry("b", 2f, pinned: true),
        };

        Assert.That(ToastPolicy.IndexToEvict(toasts), Is.EqualTo(0));
    }

    [Test]
    public void Announcement_toast_names_the_role_so_players_know_who_is_speaking()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LobbyAnnouncementPolicy.FormatToast(isHost: true, "Restart soon"), Is.EqualTo("Host announcement: Restart soon"));
            Assert.That(LobbyAnnouncementPolicy.FormatToast(isHost: false, "Restart soon"), Is.EqualTo("Helper announcement: Restart soon"));
        });
    }

    [Test]
    public void Steam_ID_mismatch_alert_fires_only_when_the_count_rises()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SteamIdTruthAlertPolicy.ShouldAlert(previousMismatchCount: 0, currentMismatchCount: 1), Is.True);
            Assert.That(SteamIdTruthAlertPolicy.ShouldAlert(1, 1), Is.False);
            Assert.That(SteamIdTruthAlertPolicy.ShouldAlert(2, 1), Is.False);
            Assert.That(SteamIdTruthAlertPolicy.ShouldAlert(1, 3), Is.True);
        });
    }

    [Test]
    public void Guide_is_cozy_complete_and_uses_the_players_current_hotkeys()
    {
        IReadOnlyList<GuideSection> sections = GuideOverlayPolicy.BuildSections("0.2.5", "Home", "Insert", "End", "F8");
        string all = string.Join("\n", System.Linq.Enumerable.Select(sections, s => s.Heading + "\n" + s.Body));

        Assert.Multiple(() =>
        {
            Assert.That(sections.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(all, Does.Contain("Home").And.Contain("Insert").And.Contain("End").And.Contain("F8"));
            Assert.That(all, Does.Contain("WORK").And.Contain("RELAX"));
            Assert.That(all, Does.Contain("/announce"));
            Assert.That(all, Does.Contain("/chatvanilla"));
            Assert.That(all, Does.Contain("Toast"));
            foreach (string jargon in new[] { "PurrNet", "SteamID64", "Harmony", "BepInEx", "transport", "snapshot" })
            {
                Assert.That(all, Does.Not.Contain(jargon), "Guide must stay in cozy player terms: " + jargon);
            }
        });
    }

    // Seal attempt 2026-10-17: "a private label" contains "Private Lab" (case-insensitive), a term
    // the public-package privacy scan forbids, and blocked the seal. Guide text must stay clean.
    [Test]
    public void Guide_text_never_trips_the_public_package_privacy_terms()
    {
        string all = string.Join("\n", System.Linq.Enumerable.Select(
            GuideOverlayPolicy.BuildSections("0.2.5", "Home", "Insert", "End", "F8"), s => s.Heading + "\n" + s.Body));

        foreach (string term in new[] { "BlueExclusive", "BlueSage_OnTogether_Inspector_Private", "Private Lab", "F6 private" })
        {
            Assert.That(all.IndexOf(term, System.StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1), term);
        }
    }

    [Test]
    public void Guide_names_unbound_hotkeys_by_their_command_instead()
    {
        string all = string.Join("\n", System.Linq.Enumerable.Select(
            GuideOverlayPolicy.BuildSections("0.2.5", "None", "None", "None", "None"), s => s.Body));

        Assert.Multiple(() =>
        {
            Assert.That(all, Does.Contain("/qolmenu"));
            Assert.That(all, Does.Contain("/styleui"));
            Assert.That(all, Does.Contain("/rebind fly"));
            Assert.That(all, Does.Contain("/sweep now"));
        });
    }
}
