#nullable enable

namespace BlueSage.QoLTweaks.Core;

// Round 5h (Blue, 2026-10-25): "a lot of folks never knew" the extra outfit slots 4 to 9 exist and start on. The
// first time the avatar screen opens with the extra rows in place, one toast says so. It is shown once per player
// (saved flag) and only when it can actually be seen: with Toast Pop-ups off nothing is shown and nothing is marked.
internal static class OutfitSlotsHintPolicy
{
    internal const string Notice = "Your extra outfit slots are ready: styles 4 to 9 sit below the game's three in the style list. Pick one to make it yours.";

    internal static bool ShouldShow(bool alreadyShown, bool toastsEnabled, int extraRowsReady)
    {
        return !alreadyShown && toastsEnabled && extraRowsReady > 0;
    }
}
