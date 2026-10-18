namespace BlueSage.QoLTweaks.Core;

internal static class QolStatusTextPolicy
{
    internal const int MaxPlayerCharacters = 240;

    internal static string BuildPlayerSummary(
        bool nativeSweepDetected,
        bool blueSageSweepEnabled)
    {
        return BuildPlayerSummary(
            nativeSweepDetected
                ? NativeAssetSweepOwnershipState.NativeGame
                : NativeAssetSweepOwnershipState.BlueSage,
            blueSageSweepEnabled,
            blueSageManualControlsReady: true);
    }

    internal static string BuildPlayerSummary(
        NativeAssetSweepOwnershipState nativeSweepOwnership,
        bool blueSageSweepEnabled)
    {
        return BuildPlayerSummary(
            nativeSweepOwnership,
            blueSageSweepEnabled,
            blueSageManualControlsReady: true);
    }

    internal static string BuildPlayerSummary(
        NativeAssetSweepOwnershipState nativeSweepOwnership,
        bool blueSageAutomaticLoopReady,
        bool blueSageManualControlsReady)
    {
        string gameCleanup;
        switch (nativeSweepOwnership)
        {
            case NativeAssetSweepOwnershipState.NativeGame:
                gameCleanup = "native timer detected; unchanged";
                break;
            case NativeAssetSweepOwnershipState.InspectionIncomplete:
                gameCleanup = "inspection incomplete; native behavior unchanged";
                break;
            default:
                gameCleanup = "not detected";
                break;
        }

        string blueSageCleanup;
        if (blueSageAutomaticLoopReady && blueSageManualControlsReady)
        {
            blueSageCleanup = "automatic cleanup on; manual controls ready";
        }
        else if (blueSageAutomaticLoopReady)
        {
            blueSageCleanup = "automatic cleanup on; manual controls off";
        }
        else if (blueSageManualControlsReady)
        {
            blueSageCleanup = "automatic cleanup off; manual controls ready";
        }
        else
        {
            blueSageCleanup = "automatic cleanup off; manual controls off";
        }

        return "BlueSage QoL is ready. " +
               "Game cleanup: " + gameCleanup + "; " +
               "BlueSage cleanup: " + blueSageCleanup + ". " +
               "Comfort, chat, safety, and player tools are managed from Home or /qolmenu.";
    }

    internal static string BuildPlayerSummary(
        NativeAssetSweepOwnershipState nativeSweepOwnership,
        bool blueSageAutomaticLoopReady,
        bool blueSageManualControlsReady,
        string qolMenuKeyLabel)
    {
        string summary = BuildPlayerSummary(
            nativeSweepOwnership,
            blueSageAutomaticLoopReady,
            blueSageManualControlsReady);
        return summary.Replace(
            "Home or /qolmenu",
            HotkeyLabelPolicy.KeyOrCommand(qolMenuKeyLabel, "/qolmenu"));
    }
}
