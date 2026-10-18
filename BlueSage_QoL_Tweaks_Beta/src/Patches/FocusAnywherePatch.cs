using System.Reflection;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TaskManager), "IsReadyForFreeFocus")]
internal static class FocusAnywhereFreeFocusPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (Plugin.ShouldApplyFocusAnywhere)
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(TaskManager), "IsReadyForAreaFocus")]
internal static class FocusAnywhereAreaFocusPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (Plugin.ShouldApplyFocusAnywhere)
        {
            __result = true;
        }
    }
}

[HarmonyPatch]
internal static class FocusAnywhereIsFocusablePatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(PlayerFocusController), "IsFocusable");
    }

    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (Plugin.ShouldApplyFocusAnywhere)
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(PlayerMovementController), "get_IsLake")]
internal static class FocusAnywhereLakePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (Plugin.ShouldApplyFocusAnywhere)
        {
            __result = false;
        }
    }
}
