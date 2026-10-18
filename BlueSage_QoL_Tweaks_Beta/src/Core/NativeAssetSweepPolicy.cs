namespace BlueSage.QoLTweaks.Core;

internal enum NativeAssetSweepOwnershipState
{
    BlueSage,
    NativeGame,
    InspectionIncomplete
}

internal enum NativeSweepCoordinationDecision
{
    Skip,
    Defer,
    SatisfiedByVanilla,
    Run
}

internal static class NativeAssetSweepPolicy
{
    internal const float VanillaBoundaryWindowSeconds = 60f;

    public static bool IsPresent(
        bool mainSceneTimerPresent,
        bool settingsDurationPresent,
        bool unloadUnusedAssetsCallPresent)
    {
        return mainSceneTimerPresent &&
               settingsDurationPresent &&
               unloadUnusedAssetsCallPresent;
    }

    public static bool ShouldStandDown(
        bool mainSceneTimerPresent,
        bool settingsDurationPresent,
        bool unloadUnusedAssetsCallPresent,
        bool inspectionFailed)
    {
        return ResolveOwnership(
                   mainSceneTimerPresent,
                   settingsDurationPresent,
                   unloadUnusedAssetsCallPresent,
                   inspectionFailed) != NativeAssetSweepOwnershipState.BlueSage;
    }

    public static NativeAssetSweepOwnershipState ResolveOwnership(
        bool mainSceneTimerPresent,
        bool settingsDurationPresent,
        bool unloadUnusedAssetsCallPresent,
        bool inspectionFailed)
    {
        if (!inspectionFailed && IsPresent(
                mainSceneTimerPresent,
                settingsDurationPresent,
                unloadUnusedAssetsCallPresent))
        {
            return NativeAssetSweepOwnershipState.NativeGame;
        }

        return inspectionFailed ||
               mainSceneTimerPresent ||
               settingsDurationPresent ||
               unloadUnusedAssetsCallPresent
            ? NativeAssetSweepOwnershipState.InspectionIncomplete
            : NativeAssetSweepOwnershipState.BlueSage;
    }

    public static bool AllowBlueSageAutoSweep(
        bool nativeSweepPresent,
        bool compatibilityAllows,
        bool savedEnabled)
    {
        return compatibilityAllows && savedEnabled;
    }

    public static bool AllowBlueSageManualSweep(
        bool nativeSweepPresent,
        bool compatibilityAllows)
    {
        return compatibilityAllows;
    }

    public static NativeSweepCoordinationDecision DecideAutomaticSweep(
        bool automaticDue,
        bool blueSageSweepRunning,
        bool vanillaStateAvailable,
        float vanillaTimerSeconds,
        float vanillaDurationSeconds,
        bool vanillaSweepActive)
    {
        if (!automaticDue || blueSageSweepRunning)
        {
            return NativeSweepCoordinationDecision.Skip;
        }

        if (!vanillaStateAvailable)
        {
            return NativeSweepCoordinationDecision.Defer;
        }

        if (float.IsNaN(vanillaTimerSeconds) ||
            float.IsInfinity(vanillaTimerSeconds) ||
            float.IsNaN(vanillaDurationSeconds) ||
            float.IsInfinity(vanillaDurationSeconds) ||
            vanillaTimerSeconds < 0f ||
            vanillaDurationSeconds <= 0f)
        {
            return NativeSweepCoordinationDecision.Defer;
        }

        float secondsUntilVanillaSweep = vanillaDurationSeconds - vanillaTimerSeconds;
        if (vanillaSweepActive ||
            vanillaTimerSeconds <= VanillaBoundaryWindowSeconds)
        {
            return NativeSweepCoordinationDecision.SatisfiedByVanilla;
        }

        if (secondsUntilVanillaSweep <= VanillaBoundaryWindowSeconds)
        {
            return NativeSweepCoordinationDecision.Defer;
        }

        return NativeSweepCoordinationDecision.Run;
    }

}
