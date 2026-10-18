using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HotkeyInputGatePolicyTests
{
    [Test]
    public void Hotkeys_stand_down_while_any_text_input_is_focused()
    {
        // Real bug (ultrareview 2026-10-17): NoclipFlyKey defaults to End, which is also the
        // jump-to-end-of-line key in chat, so editing a chat line toggled fly.
        Assert.Multiple(() =>
        {
            Assert.That(HotkeyInputGatePolicy.ShouldDispatchHotkeys(textInputFocused: true), Is.False);
            Assert.That(HotkeyInputGatePolicy.ShouldDispatchHotkeys(textInputFocused: false), Is.True);
        });
    }

    [Test]
    public void Rebind_capture_ignores_keys_typed_into_a_focused_text_box()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HotkeyInputGatePolicy.ShouldEvaluateRebindCapture(textInputFocused: true), Is.False);
            Assert.That(HotkeyInputGatePolicy.ShouldEvaluateRebindCapture(textInputFocused: false), Is.True);
        });
    }

    [Test]
    public void Capture_clock_does_not_start_while_chat_still_has_focus_after_sending_the_command()
    {
        float? clock = HotkeyInputGatePolicy.AdvanceCaptureClock(null, textInputFocused: true, now: 100f);
        Assert.That(clock, Is.Null);

        clock = HotkeyInputGatePolicy.AdvanceCaptureClock(clock, textInputFocused: true, now: 130f);
        Assert.That(clock, Is.Null, "Waiting in chat for 30s must not burn the capture window.");
        Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(clock, requestedAt: 100f, now: 130f), Is.False);
    }

    [Test]
    public void Capture_clock_starts_the_first_frame_a_key_can_be_heard_and_then_holds()
    {
        float? clock = HotkeyInputGatePolicy.AdvanceCaptureClock(null, textInputFocused: false, now: 50f);
        Assert.That(clock, Is.EqualTo(50f));

        clock = HotkeyInputGatePolicy.AdvanceCaptureClock(clock, textInputFocused: true, now: 55f);
        Assert.That(clock, Is.EqualTo(50f), "Once started, refocusing chat must not restart or clear the clock.");
    }

    [Test]
    public void Capture_expires_after_the_timeout_so_hotkeys_never_stay_off_indefinitely()
    {
        float start = 10f;
        float timeout = HotkeyInputGatePolicy.RebindCaptureTimeoutSeconds;

        Assert.Multiple(() =>
        {
            Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(start, start, start + timeout - 0.01f), Is.False);
            Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(start, start, start + timeout), Is.True);
            // 2026-10-17 (ultrareview #2-1): this used to assert a never-active capture can NEVER
            // expire - that premise was the hang. It now waits only until the hard cap.
            Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(null, requestedAt: 9990f, now: 9999f), Is.False, "A never-active capture waits, but only until the hard cap.");
        });
    }

    [Test]
    public void Repeated_identical_rejections_are_announced_once()
    {
        // Real bug (ultrareview #6): every rejected key posted its own chat line and toast.
        Assert.Multiple(() =>
        {
            Assert.That(HotkeyInputGatePolicy.ShouldAnnounceRejection("W", lastAnnouncedKey: null), Is.True);
            Assert.That(HotkeyInputGatePolicy.ShouldAnnounceRejection("W", lastAnnouncedKey: "W"), Is.False);
            Assert.That(HotkeyInputGatePolicy.ShouldAnnounceRejection("A", lastAnnouncedKey: "W"), Is.True);
        });
    }
}
