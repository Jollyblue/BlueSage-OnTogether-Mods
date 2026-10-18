using System;

namespace BlueSage.QoLTweaks.Core;

// The one place that turns a configured hotkey into player-facing text. A hotkey set to None (or
// left blank) is unbound, so text points at the slash command instead of saying "NONE opens...".
internal static class HotkeyLabelPolicy
{
    internal static bool IsUnbound(string keyName)
    {
        return string.IsNullOrWhiteSpace(keyName) ||
               string.Equals(keyName.Trim(), "None", StringComparison.OrdinalIgnoreCase);
    }

    internal static string KeyOrCommand(string keyName, string command)
    {
        return IsUnbound(keyName) ? command : keyName.Trim() + " or " + command;
    }
}
