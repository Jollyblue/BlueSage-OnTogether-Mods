using System;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(DataManager), nameof(DataManager.SaveBanData))]
internal static class NativeBanAuditPatch
{
    [HarmonyPostfix]
    private static void SaveBanData_Postfix()
    {
        SessionAuditController.CapturePersistedNativeBanSave();
    }

    [HarmonyFinalizer]
    private static Exception SaveBanData_Finalizer(Exception __exception)
    {
        if (__exception != null) SessionAuditController.CancelPersistedModerationAudit();
        return __exception;
    }
}
