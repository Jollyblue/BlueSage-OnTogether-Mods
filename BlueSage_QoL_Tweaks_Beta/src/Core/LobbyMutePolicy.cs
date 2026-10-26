using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

// Round 4 Stage E #3 (Blue, 2026-10-19): mute = lobby-wide. The host stops relaying that player's chat to everyone,
// keyed by the AUTHORITATIVE network sender (RPCInfo.sender mapped to a Steam ID), never by the packet's own
// claimed id, which is spoofable. Hosting-session only; voice is unaffected. The game draws a sender's own line only
// when the server echoes it (SendMessageAsync runs server-side, runLocally: false), so the muted player's own
// messages stop appearing for them too: they will notice.
internal static class LobbyMutePolicy
{
    // Exactly one roster row may claim the sender's network id. Zero or several means the mapping is unsafe, and
    // the chat is let through: dropping chat from a sender we cannot name would silence the wrong person.
    internal static bool TryResolveSender(int matchingRowCount)
    {
        return matchingRowCount == 1;
    }

    internal static bool ShouldDrop(string senderSteamId, ICollection<string> mutedSteamIds)
    {
        return !string.IsNullOrEmpty(senderSteamId) && mutedSteamIds != null && mutedSteamIds.Contains(senderSteamId);
    }

    internal static bool IsMuteAction(string action)
    {
        return string.Equals(action, "mute", StringComparison.Ordinal) || string.Equals(action, "unmute", StringComparison.Ordinal);
    }
}
