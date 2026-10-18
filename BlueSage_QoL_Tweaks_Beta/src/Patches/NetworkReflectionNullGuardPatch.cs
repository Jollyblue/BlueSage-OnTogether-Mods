using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PurrNet;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(NetworkReflection), "ObserversRpc_Original_1")]
internal static class NetworkReflectionNullGuardPatch
{
    private static readonly FieldInfo ReflectedValuesField = AccessTools.Field(typeof(NetworkReflection), "_reflectedValues");
    private const int MaximumDistinctDiagnostics = 32;
    private static readonly HashSet<string> ReportedSurfaces = new HashSet<string>(System.StringComparer.Ordinal);

    [HarmonyPrefix]
    private static bool Prefix(NetworkReflection __instance)
    {
#if MODDERBRANCH_UAT
        if (Plugin.EnableRosterAndNetworkGuards?.Value != true)
        {
            return true;
        }
#endif

        if (__instance == null)
        {
            return false;
        }

        ICollection reflectedValues = ReflectedValuesField?.GetValue(__instance) as ICollection;
        if (reflectedValues != null)
        {
            return true;
        }

        string surfaceName = string.IsNullOrWhiteSpace(__instance.name) ? "<unnamed>" : __instance.name.Trim();
        string diagnosticKey = __instance.GetType().FullName + "|" + surfaceName;
        if (ReportedSurfaces.Count < MaximumDistinctDiagnostics && ReportedSurfaces.Add(diagnosticKey))
        {
            Plugin.Log?.LogWarning($"PurrNet NetworkReflection '{surfaceName}' received an observer update without initialized reflected values; skipped the impossible update instead of throwing every tick. Repeated instances of the same surface are suppressed.");
        }
        return false;
    }
}
