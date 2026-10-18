using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal static class HotkeyRebindPolicy
{
    // Allowlist, not a blocklist: a blocklist of "reserved" keys (WASD/Space/etc.) still let
    // through every other letter, digit, and punctuation key -- any of which types into chat or
    // a slash command and would fire the bound hotkey on every keystroke. Only genuinely
    // non-typable keys are offered: function keys, the navigation/editing cluster, arrows, and
    // lock/system keys. The numpad is excluded entirely (2026-10-17): its digits and symbols type
    // into chat, and KeypadEnter sends chat like Return. Navigation keys (Home/End/arrows/Delete)
    // still move the caret in text boxes, which is why Plugin.Update also gates every hotkey on
    // text focus (HotkeyInputGatePolicy) -- the allowlist alone can never be the whole defense.
    private static readonly HashSet<string> AllowedKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "F13", "F14", "F15",
        "Home", "End", "Insert", "Delete", "PageUp", "PageDown",
        "UpArrow", "DownArrow", "LeftArrow", "RightArrow",
        "Pause", "CapsLock", "ScrollLock", "Numlock", "Print", "SysReq", "Break", "Menu",
        "LeftWindows", "RightWindows", "AltGr",
        "LeftControl", "RightControl", "LeftShift", "RightShift", "LeftAlt", "RightAlt",
        "None"
    };

    // Fly's own movement controls plus the modifier keys fly also uses for boost/precision.
    // Allowed above for the *typing-safety* check (they're not typable into chat), but still
    // refused here because binding one would fight with fly itself the moment the player moves.
    private static readonly HashSet<string> ReservedForFlyMovement = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "LeftControl", "RightControl", "LeftShift", "RightShift", "LeftAlt", "RightAlt"
    };

    private static readonly HashSet<string> TypingKeyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Space", "Return", "Tab", "Backspace", "Escape", "KeypadEnter",
        "Comma", "Period", "Slash", "Backslash", "Semicolon", "Quote", "BackQuote", "Minus", "Equals",
        "LeftBracket", "RightBracket", "LeftCurlyBracket", "RightCurlyBracket", "Plus", "Underscore",
        "Colon", "Less", "Greater", "Question", "At", "Hash", "Dollar", "Percent", "Ampersand",
        "Asterisk", "LeftParen", "RightParen", "Caret", "Exclaim", "DoubleQuote", "Pipe", "Tilde"
    };

    // Saved (pre-existing) bindings are judged more leniently than new rebinds: only keys that type
    // into chat, or fight fly's modifiers, are unsafe. An exotic non-typable bind that already worked
    // (e.g. Mouse4) is kept rather than silently reset on every launch (Blue's call, 2026-10-17).
    internal static bool IsUnsafeSavedBinding(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return true;
        }

        string name = keyName.Trim();
        bool singleLetter = name.Length == 1 && char.IsLetter(name[0]);
        return singleLetter ||
               name.StartsWith("Alpha", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Keypad", StringComparison.OrdinalIgnoreCase) ||
               TypingKeyNames.Contains(name) ||
               ReservedForFlyMovement.Contains(name);
    }

    // A key any hotkey may hold at all, before collision checks: allowlisted and not a fly modifier.
    internal static bool IsAssignableKey(string keyName)
    {
        return !string.IsNullOrWhiteSpace(keyName) &&
               AllowedKeyNames.Contains(keyName) &&
               !ReservedForFlyMovement.Contains(keyName);
    }

    internal static bool TryValidateCandidate(
        string candidateKeyName,
        IEnumerable<string> otherBoundKeyNames,
        out string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(candidateKeyName))
        {
            rejectionReason = "No key name given.";
            return false;
        }

        if (!AllowedKeyNames.Contains(candidateKeyName))
        {
            rejectionReason = candidateKeyName + " would also fire while typing in chat or a slash command — pick a function key, Home/End/Insert/Delete/Page Up/Page Down, or an arrow key instead.";
            return false;
        }

        if (ReservedForFlyMovement.Contains(candidateKeyName))
        {
            rejectionReason = candidateKeyName + " is a fly-movement modifier BlueSage already uses — pick another.";
            return false;
        }

        // "None" means "disabled" and is allowed on more than one hotkey at once; it is not a
        // real key any two hotkeys could actually collide on.
        if (string.Equals(candidateKeyName, "None", StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = null;
            return true;
        }

        string collision = otherBoundKeyNames?.FirstOrDefault(
            other => string.Equals(other, candidateKeyName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(collision))
        {
            rejectionReason = candidateKeyName + " is already bound to another BlueSage hotkey — pick another.";
            return false;
        }

        rejectionReason = null;
        return true;
    }
}
