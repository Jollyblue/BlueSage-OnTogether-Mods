using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatClickActionPolicyTests
{
    [Test]
    public void Plain_left_click_with_exact_sender_binding_overrides_a_stale_native_host_target()
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            Context(
                rightButton: false,
                hasSenderBinding: true));

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.OpenNativeIdCard));
    }

    [Test]
    public void Plain_left_click_without_exact_sender_binding_stays_owned_by_vanilla()
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            Context(
                rightButton: false,
                hasSenderBinding: false));

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.Ignore));
    }

    [Test]
    public void Control_left_click_copies_message_without_replacing_plain_vanilla_click()
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            Context(rightButton: false, control: true));

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.CopyMessage));
    }

    [Test]
    public void Shift_left_click_uses_the_guarded_safe_link_path()
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            Context(
                rightButton: false,
                shift: true,
                linkOpeningEnabled: true,
                hasSafeUrl: true));

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.GuardedSafeLink));
    }

    [Test]
    public void Plain_right_click_copies_only_the_message()
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(Context());

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.CopyMessage));
    }

    [Test]
    public void Shift_right_click_selects_only_the_guarded_link_path()
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            Context(shift: true, linkOpeningEnabled: true, hasSafeUrl: true));

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.GuardedSafeLink));
    }

    [Test]
    public void Control_right_click_requires_an_exact_sender_binding()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(control: true, hasSenderBinding: true)).Action,
                Is.EqualTo(ChatClickAction.CopyScopedIdentityEvidence));
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(control: true, hasSenderBinding: false)).Action,
                Is.EqualTo(ChatClickAction.Denied));
        });
    }

    [Test]
    public void Alt_right_click_requires_an_exact_sender_binding()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(alt: true, hasSenderBinding: true)).Action,
                Is.EqualTo(ChatClickAction.OpenNativeIdCard));
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(alt: true, hasSenderBinding: false)).Action,
                Is.EqualTo(ChatClickAction.Denied));
        });
    }

    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    public void Multi_modifier_combinations_fail_safe(
        bool shift,
        bool control,
        bool alt)
    {
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            Context(
                shift: shift,
                control: control,
                alt: alt,
                hasSenderBinding: true));

        Assert.That(decision.Action, Is.EqualTo(ChatClickAction.Denied));
    }

    [Test]
    public void Notification_rows_keep_copy_and_link_but_never_gain_identity_actions()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(hasSenderBinding: false)).Action,
                Is.EqualTo(ChatClickAction.CopyMessage));
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(
                        shift: true,
                        linkOpeningEnabled: true,
                        hasSafeUrl: true,
                        hasSenderBinding: false)).Action,
                Is.EqualTo(ChatClickAction.GuardedSafeLink));
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(control: true, hasSenderBinding: false)).Action,
                Is.EqualTo(ChatClickAction.Denied));
            Assert.That(
                ChatClickActionPolicy.Decide(
                    Context(alt: true, hasSenderBinding: false)).Action,
                Is.EqualTo(ChatClickAction.Denied));
        });
    }

    private static ChatClickContext Context(
        bool rightButton = true,
        bool shift = false,
        bool control = false,
        bool alt = false,
        bool hasPayload = true,
        bool linkOpeningEnabled = false,
        bool hasSafeUrl = false,
        bool hasSenderBinding = false)
    {
        return new ChatClickContext
        {
            IsRightButton = rightButton,
            ShiftHeld = shift,
            ControlHeld = control,
            AltHeld = alt,
            HasPayload = hasPayload,
            LinkOpeningEnabled = linkOpeningEnabled,
            HasSafeUrl = hasSafeUrl,
            HasSenderBinding = hasSenderBinding,
        };
    }
}
