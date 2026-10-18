using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(QuadPainterGPU), "GetQuadImage_Original_1")]
internal static class ChalkboardSnapshotPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuadPainterGPU __instance)
    {
        ChalkboardSnapshotTransportController.PrepareForFullSnapshot(__instance);
    }
}
