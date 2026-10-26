using System;

namespace BlueSage.QoLTweaks.Core;

// Round 4 WS3: opening a player's ID card / Steam profile is a view-only action (the opened card's own Ban button is
// routed separately for the host by NativeBanRoutePatch, Stage C P1). When our verified identity evidence is
// unavailable (typically a player who rejoined through a reconnect mod and is missing from Steam's lobby list),
// the clicked row may still open exactly as the game would: the game's own Steam ID list and the row itself must
// both name the requested player at that index. Moderation never uses this path.
internal static class NativeIdCardReadOnlyFallbackPolicy
{
    internal static bool TryUseNativeRow(
        bool verifiedEvidenceAvailable,
        string requestedSteamId,
        int clickedRowIndex,
        string gameListSteamIdAtRow,
        string rowOwnSteamId,
        out int nativeIndex)
    {
        nativeIndex = -1;
        if (verifiedEvidenceAvailable ||
            clickedRowIndex < 0 ||
            string.IsNullOrWhiteSpace(requestedSteamId) ||
            !string.Equals(gameListSteamIdAtRow, requestedSteamId, StringComparison.Ordinal) ||
            !string.Equals(rowOwnSteamId, requestedSteamId, StringComparison.Ordinal))
        {
            return false;
        }

        nativeIndex = clickedRowIndex;
        return true;
    }
}
