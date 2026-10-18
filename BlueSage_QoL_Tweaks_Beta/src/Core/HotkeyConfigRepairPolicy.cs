using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct HotkeyBinding
{
    internal HotkeyBinding(string label, string current, string defaultKey)
    {
        Label = label;
        Current = current;
        Default = defaultKey;
    }

    internal string Label { get; }
    internal string Current { get; }
    internal string Default { get; }
}

internal sealed class HotkeyRepairResult
{
    internal HotkeyRepairResult(IReadOnlyList<string> finalKeys, IReadOnlyList<string> messages)
    {
        FinalKeys = finalKeys;
        Messages = messages;
    }

    internal IReadOnlyList<string> FinalKeys { get; }
    internal IReadOnlyList<string> Messages { get; }
    internal bool Changed => Messages.Count > 0;
}

// Validates hotkeys loaded from a player's existing config. Saved values never went through the
// rebind policy (older versions had no allowlist, and Noclip/Fly arrived later defaulting to End),
// so on load: keys that type into chat (or fight fly) go back to their default -- exotic
// non-typable binds like Mouse4 are kept -- then every collision is resolved with a
// deterministic loser -- a key still on its default yields to a player-chosen one; otherwise the
// later hotkey yields -- and the loser is turned off ("None") rather than guessed at.
internal static class HotkeyConfigRepairPolicy
{
    internal const string Disabled = "None";

    internal static HotkeyRepairResult Repair(IReadOnlyList<HotkeyBinding> bindings)
    {
        var keys = new string[bindings.Count];
        var messages = new List<string>();

        for (int i = 0; i < bindings.Count; i++)
        {
            string current = bindings[i].Current ?? string.Empty;
            if (!HotkeyRebindPolicy.IsUnsafeSavedBinding(current))
            {
                keys[i] = current;
                continue;
            }

            keys[i] = bindings[i].Default;
            messages.Add($"{bindings[i].Label} was set to \"{current}\", which would fire while typing in chat; reset to its default, {bindings[i].Default}.");
        }

        bool collisionFound = true;
        while (collisionFound)
        {
            collisionFound = false;
            for (int i = 1; i < keys.Length && !collisionFound; i++)
            {
                for (int j = 0; j < i && !collisionFound; j++)
                {
                    if (IsDisabled(keys[i]) || !Same(keys[i], keys[j]))
                    {
                        continue;
                    }

                    bool iOnDefault = Same(keys[i], bindings[i].Default);
                    bool jOnDefault = Same(keys[j], bindings[j].Default);
                    int loser = jOnDefault && !iOnDefault ? j : i;
                    int winner = loser == i ? j : i;
                    messages.Add($"{bindings[loser].Label} hotkey turned off: {keys[loser]} is already the {bindings[winner].Label} key. Pick a new one with /rebind or the menu's Hotkeys section.");
                    keys[loser] = Disabled;
                    collisionFound = true;
                }
            }
        }

        return new HotkeyRepairResult(keys, messages);
    }

    internal static string FindHolder(string key, IReadOnlyList<HotkeyBinding> others)
    {
        if (IsDisabled(key))
        {
            return null;
        }

        foreach (HotkeyBinding other in others)
        {
            if (Same(other.Current, key))
            {
                return other.Label;
            }
        }

        return null;
    }

    private static bool IsDisabled(string key) => string.IsNullOrWhiteSpace(key) || Same(key, Disabled);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
