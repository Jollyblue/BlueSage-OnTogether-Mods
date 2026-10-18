using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 3b (Blue, 2026-10-18): the welcome pop-up offers itself on EVERY lobby join (reconnects and
// lobby-hops included), with a "Don't show this again" opt-out that turns the toggle off. It used to
// offer once per plugin version, so a player who had seen 0.2.5 never got it again.
public sealed class WelcomeOverlayPolicyTests
{
    [Test]
    public void Offers_on_a_new_lobby_visit()
    {
        Assert.That(WelcomeOverlayPolicy.ShouldOffer(enabled: true, currentVisitKey: "visit-a", lastOfferedVisitKey: ""), Is.True);
    }

    [Test]
    public void Does_not_offer_twice_in_the_same_visit()
    {
        Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, "visit-a", "visit-a"), Is.False);
    }

    [Test]
    public void Offers_again_after_a_reconnect_or_lobby_hop()
    {
        // The visit key changes on every join and rejoin (lobby id + game scene instance).
        Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, "visit-b", "visit-a"), Is.True);
    }

    // Review 2026-10-18: the key's prefix can change mid-visit (code: or visible first, steam: once the
    // Steam lobby id resolves). The scene instance is what makes a visit, so that's what's compared.
    [Test]
    public void The_same_visit_stays_the_same_when_the_lobby_id_resolves_late()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, "steam:76561|scene:42", "code:ABCD|scene:42"), Is.False);
            Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, "steam:76561|scene:42", "visible|scene:42"), Is.False);
            Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, "steam:76561|scene:43", "steam:76561|scene:42"), Is.True, "A rejoin loads a new scene.");
            Assert.That(WelcomeLobbyKeyPolicy.IsSameVisit("code:ABCD|scene:42", "steam:76561|scene:42"), Is.True);
            Assert.That(WelcomeLobbyKeyPolicy.IsSameVisit("steam:1|scene:42", "steam:1|scene:43"), Is.False);
            Assert.That(WelcomeLobbyKeyPolicy.IsSameVisit("", "steam:1|scene:42"), Is.False);
            Assert.That(WelcomeLobbyKeyPolicy.IsSameVisit(null, null), Is.False);
        });
    }

    [Test]
    public void Dont_show_again_turns_it_off_for_every_visit()
    {
        Assert.That(WelcomeOverlayPolicy.ShouldOffer(enabled: false, currentVisitKey: "visit-b", lastOfferedVisitKey: "visit-a"), Is.False);
    }

    [Test]
    public void Never_offers_outside_a_lobby()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, "", "visit-a"), Is.False);
            Assert.That(WelcomeOverlayPolicy.ShouldOffer(true, null, null), Is.False);
        });
    }

    // Blue, UAT 2026-10-18: "Don't show this again" was cut off; the old fixed sum ignored the space Unity
    // keeps around each label and button. The height now counts every margin and gap.
    [Test]
    public void Window_height_counts_every_margin_and_gap()
    {
        float height = WelcomeOverlayPolicy.WindowHeight(
            paddingVertical: 32f, titleHeight: 26f, bodyHeight: 100f, buttonCount: 4, buttonHeight: 28f,
            labelMarginVertical: 4f, buttonMarginVertical: 6f);

        Assert.Multiple(() =>
        {
            // padding + title + margin + gap + body + margin + gap + 4 x (button + margin) + 3 gaps
            Assert.That(height, Is.EqualTo(32f + 26f + 4f + 6f + 100f + 4f + 10f + (4f * 34f) + (3f * 6f)));
            float oneMore = WelcomeOverlayPolicy.WindowHeight(32f, 26f, 100f, 5, 28f, 4f, 6f);
            Assert.That(oneMore - height, Is.EqualTo(28f + 6f + 6f), "Each extra button adds its height, margin and gap.");
        });
    }

    [Test]
    public void Body_mentions_all_four_hotkeys_by_their_real_configured_names_and_points_to_rebind_surfaces()
    {
        string body = WelcomeOverlayPolicy.BuildBody("0.2.5", "F7", "F9", "End", "F8");

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("F7"));
            Assert.That(body, Does.Contain("F9"));
            Assert.That(body, Does.Contain("End"));
            Assert.That(body, Does.Contain("F8"));
            Assert.That(body, Does.Contain("Hotkeys"));
            Assert.That(body, Does.Contain("/rebind"));
            Assert.That(body, Does.Contain("/qol status"));
        });
    }
}
