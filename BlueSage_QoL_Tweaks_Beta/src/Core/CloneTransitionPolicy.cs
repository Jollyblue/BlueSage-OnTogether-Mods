using System;

namespace BlueSage.QoLTweaks.Core;

[Flags]
internal enum CloneMatchKind
{
    None = 0,
    AvatarOutfit = 1,
    ExactStyledName = 2,
}

internal static class CloneTransitionPolicy
{
    public static CloneMatchKind Evaluate(
        bool culpritPreviouslyObserved,
        string previousCulpritAvatar,
        string currentCulpritAvatar,
        string previousCulpritName,
        string currentCulpritName,
        string previousVictimAvatar,
        string currentVictimAvatar,
        string previousVictimName,
        string currentVictimName,
        bool avatarFingerprintUseful,
        bool exactNameUseful)
    {
        bool avatarChanged = !culpritPreviouslyObserved ||
                             !string.Equals(previousCulpritAvatar, currentCulpritAvatar, StringComparison.Ordinal);
        bool nameChanged = !culpritPreviouslyObserved ||
                           !string.Equals(previousCulpritName, currentCulpritName, StringComparison.Ordinal);
        bool victimAvatarStable = string.Equals(previousVictimAvatar, currentVictimAvatar, StringComparison.Ordinal);
        bool victimNameStable = string.Equals(previousVictimName, currentVictimName, StringComparison.Ordinal);

        CloneMatchKind result = CloneMatchKind.None;
        if (avatarChanged && avatarFingerprintUseful && victimAvatarStable &&
            string.Equals(currentCulpritAvatar, currentVictimAvatar, StringComparison.Ordinal))
        {
            result |= CloneMatchKind.AvatarOutfit;
        }

        if (nameChanged && exactNameUseful && victimNameStable &&
            string.Equals(currentCulpritName, currentVictimName, StringComparison.Ordinal))
        {
            result |= CloneMatchKind.ExactStyledName;
        }

        return result;
    }
}
