namespace BlueSage.QoLTweaks.Core;

internal enum NativeWriteAction
{
    Allow,
    Redirect,
    Block
}

// Round 4 WS1: while a BlueSage slot 4-9 is worn, vanilla writes to the current-style data belong to
// that slot, not to the vanilla slot 1-3 that SelectedStyleIndex still points at. Our own snapshot
// restores are the one exception: they put the vanilla slot back and must reach it.
internal static class AvatarSlotWriteGuardPolicy
{
    internal static bool ShouldRedirectNativeWrite(int activeSlot, int nativeSlotCount, int totalSlotCount, bool ownWriteInProgress)
    {
        return !ownWriteInProgress && activeSlot >= nativeSlotCount && activeSlot < totalSlotCount;
    }

    // Review R12 (2026-10-19): a write while a slot 4-9 is worn either comes from one of the game's own outfit
    // writers (SetLemonadeHat, CloseAvatarPanel: redirected into the worn slot) or from anything else, which is
    // blocked: the game's autosave would otherwise keep that outfit in vanilla slot 1-3. Rendering never reads
    // the property, so a blocked write changes no visuals.
    internal static NativeWriteAction Decide(int activeSlot, int nativeSlotCount, int totalSlotCount, bool ownWriteInProgress, bool insideNativeWriterScope)
    {
        if (!ShouldRedirectNativeWrite(activeSlot, nativeSlotCount, totalSlotCount, ownWriteInProgress))
        {
            return NativeWriteAction.Allow;
        }

        return insideNativeWriterScope ? NativeWriteAction.Redirect : NativeWriteAction.Block;
    }
}
