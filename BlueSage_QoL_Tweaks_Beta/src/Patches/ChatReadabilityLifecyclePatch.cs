using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(UIManager), "Awake")]
internal static class ChatReadabilityUiAwakePatch
{
    [HarmonyPostfix]
    private static void Awake_Postfix()
    {
        ChatReadabilityPatch.RequestRefreshSoon();
    }
}

[HarmonyPatch(typeof(UIManager))]
internal static class ChatReadabilityFontOutlinePatch
{
    private static bool Prepare()
    {
        return AccessTools.Method(typeof(UIManager), "SetMessageFontOutline", new[] { typeof(bool) }) != null;
    }

    [HarmonyPostfix]
    [HarmonyPatch("SetMessageFontOutline", typeof(bool))]
    private static void SetMessageFontOutline_Postfix(bool value)
    {
        ChatReadabilityPatch.RequestRefreshSoon();
    }
}

[HarmonyPatch(typeof(TextChannelManager), "ResetTextMaterials")]
internal static class ChatReadabilityTextMaterialResetPatch
{
    [HarmonyPostfix]
    private static void ResetTextMaterials_Postfix()
    {
        ChatReadabilityPatch.RequestRefreshSoon();
    }
}
