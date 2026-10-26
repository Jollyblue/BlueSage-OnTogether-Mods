using System;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

// Round 4 Stage C, review P1 (ponytail-review of 2911bf9): the game's three Ban buttons (ID card, player row, report
// confirm; decompiled build 25966942) ban by the first PlayerSteamIDs row matching the id, which after a reconnect is
// the stale row, so the live player stays. For the host, each click now takes the same host-verified path as /sidban
// (SteamIdModerationController.TryExecuteHostAction: live rows from the PurrNet connection table, RECONNECTED and
// LOADING labels, fail closed). Non-hosts are untouched: vanilla already disables these buttons for them.
internal static class NativeBanRouter
{
    // Returns true when the host-verified ban succeeded. handled=false means the policy chose Vanilla and the
    // original method must run; handled=true means the click was ours (routed or refused) and vanilla must not run.
    internal static bool TryRouteHostBan(string surface, string steamId, out bool handled)
    {
        handled = false;
        NativeBanRoute route = NativeBanRoutePolicy.Decide(IsVanillaHostGate(), steamId);
        if (route == NativeBanRoute.Vanilla)
        {
            return false;
        }

        handled = true;
        if (route == NativeBanRoute.Refuse)
        {
            Notify(surface, "No Steam ID is attached to this " + surface.ToLowerInvariant() + ", so nothing was banned.", warning: true);
            return false;
        }

        try
        {
            if (!SteamIdModerationController.TryGetOwnedHostLobby(out _, out string ownerSteamId))
            {
                Notify(surface, "Only the active lobby host can ban from here.", warning: true);
                return false;
            }

            bool banned;
            string message;
            HostJournalController.BeginSurface(surface);
            try
            {
                banned = SteamIdModerationController.TryExecuteHostAction("ban", steamId, "none", ownerSteamId, out message);
            }
            finally
            {
                HostJournalController.EndSurface();
            }

            Plugin.Log?.LogInfo("[BlueSage] " + surface + " ban for Steam " + steamId + " went through the host check: " + message);
            Notify(surface, message, warning: !banned);
            return banned;
        }
        catch (Exception ex)
        {
            // Fail closed: an exception here must never fall through to vanilla's first-matching-row path.
            Plugin.Log?.LogWarning("[BlueSage] " + surface + " ban for Steam " + steamId + " failed before any ban was made: " + ex.GetType().Name + ": " + ex.Message);
            Notify(surface, "The ban could not be checked (" + ex.GetType().Name + "). Nothing was banned.", warning: true);
            return false;
        }
    }

    // Routing follows the SAME gate vanilla uses to enable these buttons (TextChannelManager.isHost, the PurrNet host),
    // not our stricter Steam-owner check. Advisor at the C gate: if the stricter check said "not host" while vanilla's
    // button was enabled (Steam lobby ownership drifted, a Steam read failed), the click would fall through to the
    // stale-row path silently. This way every host click is ours, and TryExecuteHostAction's own owner check refuses
    // with a visible message when the Steam side disagrees.
    private static bool IsVanillaHostGate()
    {
        try
        {
            TextChannelManager textChannelManager = NetworkSingleton<TextChannelManager>.I;
            if (textChannelManager != null)
            {
                return textChannelManager.isHost;
            }

            return PurrNet.NetworkManager.main != null && PurrNet.NetworkManager.main.isHost;
        }
        catch (Exception)
        {
            // Unknown host state: treat as host so the click is refused by the host check rather than guessed by vanilla.
            return true;
        }
    }

    private static void Notify(string surface, string detail, bool warning)
    {
        Plugin.AddLocalNotificationWithToast(Plugin.FormatBlueSageAction(surface + " ban", detail, warning));
    }

    // Vanilla's own UI follow-up (close the popup, hide the card or row) is cosmetic; it must never undo a ban.
    internal static void Quietly(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("[BlueSage] ban UI follow-up skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(TemporaryPhoto), nameof(TemporaryPhoto.ButtonBan))]
internal static class NativeIdCardBanRoutePatch
{
    private static readonly FieldInfo PlayerSteamIdField = AccessTools.Field(typeof(TemporaryPhoto), "_playerSteamId");
    private static readonly FieldInfo BanButtonField = AccessTools.Field(typeof(TemporaryPhoto), "_banButton");

    [HarmonyPrefix]
    private static bool Prefix(TemporaryPhoto __instance)
    {
        bool banned = NativeBanRouter.TryRouteHostBan("ID card", PlayerSteamIdField?.GetValue(__instance) as string, out bool handled);
        if (!handled)
        {
            return true;
        }

        if (banned)
        {
            // Same follow-up as vanilla's ButtonBan: close the info popup, hide the card, grey the button.
            NativeBanRouter.Quietly(() =>
            {
                __instance.CloseButtonInfoPopup();
                __instance.gameObject.SetActive(false);
                if (BanButtonField?.GetValue(__instance) is Button button)
                {
                    button.interactable = false;
                }
            });
        }

        return false;
    }
}

[HarmonyPatch(typeof(PlayerItemController), nameof(PlayerItemController.ButtonBan))]
internal static class NativePlayerRowBanRoutePatch
{
    private static readonly FieldInfo PlayerSteamIdField = AccessTools.Field(typeof(PlayerItemController), "_playerSteamId");

    [HarmonyPrefix]
    private static bool Prefix(PlayerItemController __instance)
    {
        bool banned = NativeBanRouter.TryRouteHostBan("Player row", PlayerSteamIdField?.GetValue(__instance) as string, out bool handled);
        if (!handled)
        {
            return true;
        }

        if (banned)
        {
            NativeBanRouter.Quietly(() =>
            {
                NetworkSingleton<PlayerPanelController>.I?.CloseButtonInfoPopup();
                __instance.gameObject.SetActive(false);
            });
        }

        return false;
    }
}

[HarmonyPatch(typeof(PlayerPanelController), nameof(PlayerPanelController.ButtonBan))]
internal static class NativeReportBanRoutePatch
{
    private static readonly FieldInfo CurrentReportDataField = AccessTools.Field(typeof(PlayerPanelController), "_currentReportData");
    private static readonly FieldInfo BanPanelField = AccessTools.Field(typeof(PlayerPanelController), "_banPanel");

    [HarmonyPrefix]
    private static bool Prefix(PlayerPanelController __instance)
    {
        string steamId = CurrentReportDataField?.GetValue(__instance) is ReportData report ? report.SteamId : null;
        bool banned = NativeBanRouter.TryRouteHostBan("Report", steamId, out bool handled);
        if (!handled)
        {
            return true;
        }

        if (banned)
        {
            // Same follow-up as vanilla: close the report panel and show the next waiting report, if any.
            NativeBanRouter.Quietly(() =>
            {
                if (BanPanelField?.GetValue(__instance) is GameObject panel)
                {
                    panel.SetActive(false);
                }

                __instance.CheckWaitingReport();
            });
        }

        return false;
    }
}
