using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager))]
internal static class ChatTimestampPatch
{
    [HarmonyPatch("AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]
    [HarmonyPrefix]
    private static void AddMessageUiPrefix(ref string userName)
    {
        if (!Plugin.ShouldApplyChatTimestamps || string.IsNullOrEmpty(userName))
        {
            return;
        }

        userName = Plugin.BuildTimestampPrefix() + userName;
    }

    [HarmonyPatch("AddNotification")]
    [HarmonyPrefix]
    private static void AddNotificationPrefix(ref string text)
    {
        if (!Plugin.ShouldApplyNotificationTimestamps || string.IsNullOrEmpty(text))
        {
            return;
        }

        text = Plugin.BuildTimestampPrefix() + text;
    }
}
