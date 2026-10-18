using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HotkeyRebindPolicyTests
{
    [TestCase("Space")]
    [TestCase("W")]
    [TestCase("A")]
    [TestCase("S")]
    [TestCase("D")]
    [TestCase("E")]
    [TestCase("Q")]
    [TestCase("Return")]
    [TestCase("KeypadEnter")]
    [TestCase("Tab")]
    [TestCase("Backspace")]
    [TestCase("Escape")]
    public void Rejects_every_fly_movement_and_typing_key(string reservedKey)
    {
        bool ok = HotkeyRebindPolicy.TryValidateCandidate(reservedKey, new string[0], out string reason);

        Assert.That(ok, Is.False);
        Assert.That(reason, Is.Not.Null.And.Not.Empty);
    }

    [TestCase("F")]
    [TestCase("R")]
    [TestCase("T")]
    [TestCase("Z")]
    [TestCase("Alpha1")]
    [TestCase("Alpha0")]
    [TestCase("Slash")]
    [TestCase("Backslash")]
    [TestCase("Comma")]
    [TestCase("Period")]
    [TestCase("Semicolon")]
    [TestCase("Quote")]
    [TestCase("BackQuote")]
    [TestCase("Equals")]
    [TestCase("Minus")]
    public void Rejects_every_other_letter_digit_and_punctuation_key_that_would_type_into_chat(string typableKey)
    {
        // This is the real safety gap a reserved-list (WASD/Space/etc. only) misses: every other
        // letter, digit, and punctuation key still types text into chat or a slash command, and
        // would fire the bound hotkey on every keystroke.
        bool ok = HotkeyRebindPolicy.TryValidateCandidate(typableKey, new string[0], out string reason);

        Assert.That(ok, Is.False, typableKey + " must be rejected -- it can be typed in chat.");
        Assert.That(reason, Does.Contain("typing"));
    }

    [Test]
    public void Rejects_fly_movement_modifiers_even_though_they_are_not_typable()
    {
        bool ok = HotkeyRebindPolicy.TryValidateCandidate("LeftShift", new string[0], out string reason);

        Assert.That(ok, Is.False);
        Assert.That(reason, Does.Contain("fly-movement"));
    }

    [Test]
    public void Rejects_a_key_already_bound_to_another_hotkey()
    {
        bool ok = HotkeyRebindPolicy.TryValidateCandidate("F9", new[] { "Home", "F9", "Insert" }, out string reason);

        Assert.That(ok, Is.False);
        Assert.That(reason, Does.Contain("F9"));
    }

    [Test]
    public void Collision_check_is_case_insensitive()
    {
        bool ok = HotkeyRebindPolicy.TryValidateCandidate("f9", new[] { "F9" }, out string reason);

        Assert.That(ok, Is.False);
    }

    [Test]
    public void None_is_exempt_from_collision_since_it_means_disabled_not_a_real_key()
    {
        // Two different hotkeys can both legitimately be set to None (disabled) at once; that is
        // not the same as two hotkeys fighting over one physical key.
        bool ok = HotkeyRebindPolicy.TryValidateCandidate("None", new[] { "None", "F8" }, out string reason);

        Assert.That(ok, Is.True);
        Assert.That(reason, Is.Null);
    }

    [TestCase("F9")]
    [TestCase("End")]
    [TestCase("Home")]
    [TestCase("Insert")]
    [TestCase("PageUp")]
    [TestCase("PageDown")]
    [TestCase("Delete")]
    [TestCase("UpArrow")]
    public void Accepts_a_free_non_typable_key(string candidate)
    {
        bool ok = HotkeyRebindPolicy.TryValidateCandidate(candidate, new[] { "F8" }, out string reason);

        Assert.That(ok, Is.True);
        Assert.That(reason, Is.Null);
    }

    // 2026-10-17 (ultrareview #1): numpad keys type digits and symbols into chat, so they were
    // never "non-typable" despite the old allowlist comment. Flipped from accepted to rejected.
    [TestCase("Keypad0")]
    [TestCase("Keypad5")]
    [TestCase("Keypad9")]
    [TestCase("KeypadPeriod")]
    [TestCase("KeypadDivide")]
    [TestCase("KeypadMultiply")]
    [TestCase("KeypadMinus")]
    [TestCase("KeypadPlus")]
    [TestCase("KeypadEquals")]
    public void Rejects_numpad_keys_because_they_type_into_chat(string numpadKey)
    {
        bool ok = HotkeyRebindPolicy.TryValidateCandidate(numpadKey, new string[0], out string reason);

        Assert.That(ok, Is.False);
        Assert.That(reason, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Rejects_an_empty_or_missing_key_name()
    {
        Assert.That(HotkeyRebindPolicy.TryValidateCandidate("", new string[0], out _), Is.False);
        Assert.That(HotkeyRebindPolicy.TryValidateCandidate(null, new string[0], out _), Is.False);
    }
}
