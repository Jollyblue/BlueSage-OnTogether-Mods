namespace BlueSage.QoLTweaks.Core;

// Round 5e: which identity generation keys the extension registry (host capability + badge contribution).
// The companion generation is built only from rows that are in the Steam lobby, so a stray row outside the lobby
// (a ghost, a late leaver, a spoofed name) no longer locks the host's own F6 label or the badge read for everyone.
// The authority generation stays the lobby-wide gate for pruning, Clone Shield and absence checks.
internal static class HostAuthorityRegistryKeyPolicy
{
    // Rescue lane: true restores the 0.2.6 coupling (registry keyed to the lobby-wide authority generation).
    internal const bool LockedPerRowHostAuthority = false;

    internal static long RegistryGeneration(
        bool lockedPerRowHostAuthority,
        bool companionContextValid,
        long companionGeneration,
        bool authorityContextValid,
        long authorityGeneration)
    {
        if (lockedPerRowHostAuthority)
        {
            return authorityContextValid && authorityGeneration > 0 ? authorityGeneration : 0L;
        }

        return companionContextValid && companionGeneration > 0 ? companionGeneration : 0L;
    }

    internal static long FreshnessGeneration(
        bool lockedPerRowHostAuthority,
        long companionGeneration,
        long authorityGeneration) =>
        lockedPerRowHostAuthority ? authorityGeneration : companionGeneration;

    internal static string KeyName(bool lockedPerRowHostAuthority) =>
        lockedPerRowHostAuthority ? "authority" : "companion";
}
