namespace BlueSage.QoLTweaks.Core;

// Round 4 review R5 (2026-10-19): when the avatar slot store exists but cannot be read, the session runs
// read-only so nothing on disk changes. The player is told once, in game, the first time the dropdown opens.
internal static class AvatarSlotStoreNoticePolicy
{
    internal static bool ShouldShowReadOnlyNotice(bool storeLoaded, bool storeWritable, bool alreadyShown)
    {
        return storeLoaded && !storeWritable && !alreadyShown;
    }
}
