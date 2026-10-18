using System;
using HarmonyLib;
using PurrNet;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

internal static class BetterMoveState
{
    internal static bool IsSlowWalkActive { get; set; }
}

[HarmonyPatch(typeof(PlayerMovementController))]
internal static class BetterMoveMovementPatch
{
    [HarmonyPrefix]
    [HarmonyPatch("Update")]
    private static void Update_Prefix(PlayerMovementController __instance)
    {
        if (!Plugin.ShouldApplyBetterMove || !IsLocalOwner(__instance))
        {
            BetterMoveState.IsSlowWalkActive = false;
            return;
        }

        bool shift = MonoSingleton<InputManager>.I != null && MonoSingleton<InputManager>.I.IsShiftKey;
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        __instance.IsTurbo = shift;
        BetterMoveState.IsSlowWalkActive = ctrl && !shift;
    }

    [HarmonyPrefix]
    [HarmonyPatch("MovePlayer")]
    private static void MovePlayer_Prefix(PlayerMovementController __instance)
    {
        if (!Plugin.ShouldApplyBetterMove || !BetterMoveState.IsSlowWalkActive || !IsLocalOwner(__instance))
        {
            return;
        }

        SetLocalPopsicle(__instance, true);
    }

    [HarmonyPostfix]
    [HarmonyPatch("MovePlayer")]
    private static void MovePlayer_Postfix(PlayerMovementController __instance)
    {
        if (!Plugin.ShouldApplyBetterMove || !BetterMoveState.IsSlowWalkActive || !IsLocalOwner(__instance))
        {
            return;
        }

        SetLocalPopsicle(__instance, false);
    }

    internal static bool IsLocalOwner(Component component)
    {
        try
        {
            return component is NetworkIdentity identity && identity.isOwner;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug($"BetterMove owner check skipped: {ex.Message}");
            return false;
        }
    }

    private static void SetLocalPopsicle(Component component, bool active)
    {
        try
        {
            PlayerFXController fx = component.GetComponent<PlayerFXController>();
            if (fx == null)
            {
                return;
            }

            fx.IsPopsicleDouble = active;
            fx.IsFreeze = false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug($"BetterMove MovePlayer visual guard skipped: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(PlayerAnimationController), "SetWalk")]
internal static class BetterMoveAnimationPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerAnimationController __instance)
    {
        if (!Plugin.ShouldApplyBetterMove || !BetterMoveState.IsSlowWalkActive || !BetterMoveMovementPatch.IsLocalOwner(__instance))
        {
            return;
        }

        SetAnimationPopsicle(__instance, true);
    }

    [HarmonyPostfix]
    private static void Postfix(PlayerAnimationController __instance)
    {
        if (!Plugin.ShouldApplyBetterMove || !BetterMoveState.IsSlowWalkActive || !BetterMoveMovementPatch.IsLocalOwner(__instance))
        {
            return;
        }

        SetAnimationPopsicle(__instance, false);
    }

    private static void SetAnimationPopsicle(PlayerAnimationController instance, bool active)
    {
        try
        {
            PlayerFXController fx = AccessTools.Field(typeof(PlayerAnimationController), "_playerFXController")?.GetValue(instance) as PlayerFXController;
            if (fx == null)
            {
                return;
            }

            fx.IsPopsicleDouble = active;
            fx.IsFreeze = false;
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug($"BetterMove SetWalk visual guard skipped: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(PlayerFXController), "SetPopsicle")]
internal static class BetterMovePopsiclePatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return !Plugin.ShouldApplyBetterMove || !BetterMoveState.IsSlowWalkActive;
    }
}
