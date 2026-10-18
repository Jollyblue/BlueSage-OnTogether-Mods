using System.Linq;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Ultrareview #4 (2026-10-17): hotkeys already saved in a player's config were never validated on
// load. Someone who had ManualSweepKey=End before Noclip/Fly existed got End doing both.
public sealed class HotkeyConfigRepairPolicyTests
{
    private static HotkeyBinding[] Bindings(string menu, string style, string fly, string sweep) => new[]
    {
        new HotkeyBinding("QoL Menu", menu, "Home"),
        new HotkeyBinding("Style Helper", style, "Insert"),
        new HotkeyBinding("Noclip/Fly", fly, "End"),
        new HotkeyBinding("Manual Sweep", sweep, "F8"),
    };

    [Test]
    public void Defaults_need_no_repair()
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("Home", "Insert", "End", "F8"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Messages, Is.Empty);
            Assert.That(result.FinalKeys, Is.EqualTo(new[] { "Home", "Insert", "End", "F8" }));
        });
    }

    [Test]
    public void A_user_chosen_key_beats_a_new_default_that_collides_with_it()
    {
        // The exact reported case: sweep was user-set to End; fly arrived later defaulting to End.
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("Home", "Insert", "End", "End"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Changed, Is.True);
            Assert.That(result.FinalKeys, Is.EqualTo(new[] { "Home", "Insert", "None", "End" }));
            Assert.That(result.Messages.Single(), Does.Contain("Noclip/Fly").And.Contain("Manual Sweep").And.Contain("/rebind"));
        });
    }

    [Test]
    public void When_neither_colliding_key_is_a_default_the_later_one_loses()
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("F5", "F5", "End", "F8"));

        Assert.That(result.FinalKeys, Is.EqualTo(new[] { "F5", "None", "End", "F8" }));
    }

    [Test]
    public void An_unsafe_typing_key_resets_to_its_default()
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("F", "Insert", "End", "F8"));

        Assert.Multiple(() =>
        {
            Assert.That(result.FinalKeys[0], Is.EqualTo("Home"));
            Assert.That(result.Messages.Single(), Does.Contain("QoL Menu").And.Contain("F").And.Contain("Home"));
        });
    }

    // Ultrareview #2-6 (2026-10-17), Blue's call: repair only resets keys that type into chat (or
    // fight fly). An exotic non-typable bind that worked before -- e.g. ManualSweepKey=Mouse4 --
    // must survive startup instead of being silently reset on every launch.
    [TestCase("Mouse4")]
    [TestCase("Mouse3")]
    [TestCase("JoystickButton0")]
    [TestCase("F12")]
    public void Non_typable_saved_keys_survive_startup_repair(string savedKey)
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("Home", "Insert", "End", savedKey));

        Assert.Multiple(() =>
        {
            Assert.That(result.Changed, Is.False);
            Assert.That(result.FinalKeys[3], Is.EqualTo(savedKey));
        });
    }

    [TestCase("F")]
    [TestCase("Alpha3")]
    [TestCase("Keypad5")]
    [TestCase("Space")]
    [TestCase("Return")]
    [TestCase("Slash")]
    [TestCase("Escape")]
    [TestCase("LeftShift")]
    public void Typing_and_fly_modifier_keys_are_still_reset(string savedKey)
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("Home", "Insert", "End", savedKey));

        Assert.That(result.FinalKeys[3], Is.EqualTo("F8"));
    }

    [Test]
    public void A_reset_to_default_that_then_collides_is_resolved_too()
    {
        // Menu "F" resets to Home, but the player already moved Style Helper to Home (user-chosen),
        // so the menu's restored default is the one that yields.
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("F", "Home", "End", "F8"));

        Assert.That(result.FinalKeys, Is.EqualTo(new[] { "None", "Home", "End", "F8" }));
    }

    [Test]
    public void Several_disabled_hotkeys_never_count_as_a_collision()
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("None", "None", "End", "F8"));

        Assert.That(result.Changed, Is.False);
    }

    [Test]
    public void Collisions_are_case_insensitive()
    {
        HotkeyRepairResult result = HotkeyConfigRepairPolicy.Repair(Bindings("Home", "Insert", "end", "End"));

        Assert.That(result.FinalKeys.Count(k => string.Equals(k, "End", System.StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
    }

    [TestCase("Home", "Insert", "End", "End")]
    [TestCase("F", "Home", "End", "F8")]
    [TestCase("F5", "F5", "F5", "F5")]
    [TestCase("W", "Space", "Keypad5", "Return")]
    public void Repair_is_idempotent(string menu, string style, string fly, string sweep)
    {
        HotkeyRepairResult first = HotkeyConfigRepairPolicy.Repair(Bindings(menu, style, fly, sweep));
        string[] k = first.FinalKeys.ToArray();
        HotkeyRepairResult second = HotkeyConfigRepairPolicy.Repair(Bindings(k[0], k[1], k[2], k[3]));

        Assert.Multiple(() =>
        {
            Assert.That(second.Changed, Is.False);
            Assert.That(second.FinalKeys, Is.EqualTo(first.FinalKeys));
            Assert.That(first.FinalKeys.Where(key => key != "None").Distinct(System.StringComparer.OrdinalIgnoreCase).Count(),
                Is.EqualTo(first.FinalKeys.Count(key => key != "None")), "No two live hotkeys may share a key after repair.");
        });
    }

    [Test]
    public void Finds_which_hotkey_already_holds_a_key_for_the_reset_refusal_message()
    {
        // Ultrareview #5: "/rebind <x> reset" must refuse and name the holder instead of
        // silently creating a collision.
        HotkeyBinding[] others = Bindings("End", "Insert", "F7", "F8").Where(b => b.Label != "Noclip/Fly").ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(HotkeyConfigRepairPolicy.FindHolder("End", others), Is.EqualTo("QoL Menu"));
            Assert.That(HotkeyConfigRepairPolicy.FindHolder("F12", others), Is.Null);
            Assert.That(HotkeyConfigRepairPolicy.FindHolder("None", others), Is.Null);
        });
    }
}
