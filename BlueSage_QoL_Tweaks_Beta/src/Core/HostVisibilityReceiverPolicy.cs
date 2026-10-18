using System;

namespace BlueSage.QoLTweaks.Core;

internal static class HostVisibilityReceiverPolicy
{
    public static bool ShouldConsumeRemoteOwnerState(string ownerSteamId, string localSteamId)
    {
        if (string.IsNullOrWhiteSpace(ownerSteamId) || string.IsNullOrWhiteSpace(localSteamId))
        {
            return false;
        }

        return !string.Equals(ownerSteamId, localSteamId, StringComparison.Ordinal);
    }
}
