using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Ultrareview #2-1 (2026-10-17): IMGUI never clears GUIUtility.keyboardControl when a button is
// clicked, so after "type a value, press Save" the text-focus guard stayed true and Home/Insert/
// End/F8 died until the window closed. Standard IMGUI blur-on-click: a mouse-down clears keyboard
// focus; a text field under the click takes it straight back.
public sealed class TextFocusBlurPolicyTests
{
    [Test]
    public void A_click_inside_a_BlueSage_window_blurs_so_buttons_release_text_focus()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TextFocusBlurPolicy.ShouldBlurInsideWindow(isMouseDown: true), Is.True);
            Assert.That(TextFocusBlurPolicy.ShouldBlurInsideWindow(isMouseDown: false), Is.False, "Key presses must not blur the field being typed in.");
        });
    }

    [Test]
    public void A_click_outside_every_BlueSage_window_blurs()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TextFocusBlurPolicy.ShouldBlurOutsideWindows(isMouseDown: true, pointerOverBlueSageWindow: false), Is.True);
            Assert.That(TextFocusBlurPolicy.ShouldBlurOutsideWindows(isMouseDown: true, pointerOverBlueSageWindow: true), Is.False, "Clicks inside a window are handled by that window, where a text field can re-take focus.");
            Assert.That(TextFocusBlurPolicy.ShouldBlurOutsideWindows(isMouseDown: false, pointerOverBlueSageWindow: false), Is.False);
        });
    }

    [Test]
    public void Rebind_capture_has_a_hard_cap_even_if_text_focus_never_releases()
    {
        // Ultrareview #2-1, second half: with focus stuck, the 15s capture clock never started and
        // capture hung (keys and Esc ignored). The hard cap from the request time always releases it.
        float requestedAt = 100f;
        float cap = HotkeyInputGatePolicy.RebindCaptureHardCapSeconds;

        Assert.Multiple(() =>
        {
            Assert.That(cap, Is.EqualTo(60f));
            Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(null, requestedAt, requestedAt + cap - 0.01f), Is.False);
            Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(null, requestedAt, requestedAt + cap), Is.True, "Never-active capture still expires at the hard cap.");
            Assert.That(HotkeyInputGatePolicy.IsRebindCaptureExpired(110f, requestedAt, 110f + HotkeyInputGatePolicy.RebindCaptureTimeoutSeconds), Is.True, "The normal 15s timeout still applies once active.");
        });
    }
}
