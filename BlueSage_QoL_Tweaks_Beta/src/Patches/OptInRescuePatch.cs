using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerMovementController), "Update")]
internal static class OptInRescuePatch
{
    private static Transform _ownerTransform;
    private static float _cooldownUntil;
    private static float _hookUntil;
    private static string _authorizedOwnerSteam = string.Empty;
    private static RescueFollowLease _followLease;
    private static float _nextMemberDataPollAt;
    private static float _nextHookForceSyncAt;
    private static readonly HashSet<string> SeenMemberDataNonces = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Queue<string> SeenMemberDataNonceOrder = new Queue<string>();

    internal static bool IsReceiverAvailable =>
        Plugin.Instance != null &&
        NetworkSingleton<TextChannelManager>.I?.MainMovementController != null;

    internal static string Stop()
    {
        _ownerTransform = null;
        _hookUntil = 0f;
        _authorizedOwnerSteam = string.Empty;
        _followLease = default;
        _nextHookForceSyncAt = 0f;
        return "Rescue follow stopped.";
    }

    private static void PollMemberData()
    {
        if (Time.unscaledTime < _nextMemberDataPollAt) return;
        _nextMemberDataPollAt = Time.unscaledTime + 0.25f;
        try
        {
            if (!SteamManager.Initialized || !Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil) return;
            string localSteam = ((ulong)SteamUser.GetSteamID()).ToString();
            string localPlayerId = NetworkSingleton<TextChannelManager>.I?.MainNetTransform?.owner.ToString() ?? string.Empty;
            string ownerSteam = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
            var authorizedSenders = new HashSet<string>(StringComparer.Ordinal) { ownerSteam };
            string[] helperParts = (SteamMatchmaking.GetLobbyData(lobby, "bluesage_rescue_helpers") ?? string.Empty).Split(new[] { '|' }, 2);
            if (helperParts.Length == 2 && string.Equals(helperParts[0], ownerSteam, StringComparison.Ordinal))
            {
                foreach (string helper in helperParts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    authorizedSenders.Add(helper.Trim());
                }
            }

            foreach (string senderSteam in authorizedSenders)
            {
                if (!ulong.TryParse(senderSteam, out ulong senderValue)) continue;
                CSteamID sender = new CSteamID(senderValue);
                string payload = SteamMatchmaking.GetLobbyMemberData(lobby, sender, RescueProtocolPolicy.MemberDataKey);
                if (!RescueProtocolPolicy.TryParseMemberData(payload, out RescueRequest request) ||
                    !RescueProtocolPolicy.IsLocalTarget(request, localSteam, localPlayerId) ||
                    SeenMemberDataNonces.Contains(request.Nonce)) continue;
                if (!RescueProtocolPolicy.IsFresh(request, DateTimeOffset.UtcNow.ToUnixTimeSeconds())) continue;
                if (!IsAuthorizedMemberDataSender(lobby, ownerSteam, senderSteam)) continue;
                if (!IsCurrentLobbyMember(lobby, sender)) continue;
                RescueApplyResult applyResult = ApplyMemberDataRequest(lobby, ownerSteam, senderSteam, request);
                if (RescueProtocolPolicy.ShouldRememberNonce(applyResult))
                {
                    RememberNonce(request.Nonce);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("BlueSage hidden rescue poll failed closed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool IsCurrentLobbyMember(CSteamID lobby, CSteamID expected)
    {
        int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int index = 0; index < memberCount; index++)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(lobby, index) == expected) return true;
        }
        return false;
    }

    private static bool IsAuthorizedMemberDataSender(CSteamID lobby, string ownerSteam, string senderSteam)
    {
        if (string.Equals(ownerSteam, senderSteam, StringComparison.Ordinal)) return true;
        string[] parts = (SteamMatchmaking.GetLobbyData(lobby, "bluesage_rescue_helpers") ?? string.Empty).Split(new[] { '|' }, 2);
        return parts.Length == 2 && string.Equals(parts[0], ownerSteam, StringComparison.Ordinal) && Array.IndexOf(parts[1].Split(','), senderSteam) >= 0;
    }

    private static RescueApplyResult ApplyMemberDataRequest(
        CSteamID lobby,
        string ownerSteam,
        string senderSteam,
        RescueRequest request)
    {
        if (request.Action == "hooklong" && !string.Equals(SteamMatchmaking.GetLobbyData(lobby, "bluesage_rescue_longhaul"), ownerSteam + "|1", StringComparison.Ordinal)) return RescueApplyResult.Rejected;
        if (request.Action == "unhook")
        {
            Stop();
            Plugin.AddLocalNotificationWithToast("The host stopped guiding you. You can move freely.");
            return RescueApplyResult.Applied;
        }
        if (Time.unscaledTime < _cooldownUntil) return RescueApplyResult.Deferred;
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        TextChannelManager chat = NetworkSingleton<TextChannelManager>.I;
        if (panel?.PlayerSteamIDs == null || panel.PlayerTransforms == null || chat?.MainMovementController == null) return RescueApplyResult.Deferred;
        int ownerIndex = -1;
        int ownerMatches = 0;
        for (int index = 0; index < panel.PlayerSteamIDs.Count && index < panel.PlayerTransforms.Count; index++)
        {
            if (!string.Equals(panel.PlayerSteamIDs[index], ownerSteam, StringComparison.Ordinal)) continue;
            Transform candidate = panel.PlayerTransforms[index]?.transform;
            if (candidate == null || candidate.gameObject == null || !candidate.gameObject.activeInHierarchy) continue;
            ownerIndex = index;
            ownerMatches++;
        }
        if (ownerMatches != 1 || ownerIndex < 0) return RescueApplyResult.Deferred;
        _ownerTransform = panel.PlayerTransforms[ownerIndex]?.transform;
        if (_ownerTransform == null) return RescueApplyResult.Deferred;
        _authorizedOwnerSteam = ownerSteam;
        _followLease = new RescueFollowLease(
            ((ulong)lobby).ToString(),
            ownerSteam,
            senderSteam,
            request.Action == "hooklong");
        _cooldownUntil = Time.unscaledTime + RescueProtocolPolicy.CooldownSeconds;
        ApplyLocalRescuePosition(chat, _ownerTransform.position + Vector3.right * 1.25f, forceOwnerSync: true);
        _nextHookForceSyncAt = 0f;
        _hookUntil = request.Action == "hook" ? Time.unscaledTime + request.Seconds : request.Action == "hooklong" ? float.PositiveInfinity : 0f;
        Plugin.AddLocalNotificationWithToast("The host is guiding you over.");
        return RescueApplyResult.Applied;
    }

    private static void ApplyLocalRescuePosition(TextChannelManager chat, Vector3 target, bool forceOwnerSync)
    {
        if (chat?.MainMovementController == null) return;
        chat.MainMovementController.transform.position = target;
        chat.MainMovementController.SpeedY = 0f;
        if (chat.MainPlayer != null)
        {
            chat.MainPlayer.position = target;
        }
        if (forceOwnerSync) TryForceSyncLocalOwner();
    }

    private static void TryForceSyncLocalOwner()
    {
        try
        {
            NetworkSingleton<TextChannelManager>.I?.MainNetTransform?.ForceSync();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("BlueSage rescue: owner transform sync will fall back to the normal network tick: " + ex.GetType().Name + ".");
        }
    }

    private static void RememberNonce(string nonce)
    {
        if (!SeenMemberDataNonces.Add(nonce)) return;
        SeenMemberDataNonceOrder.Enqueue(nonce);
        while (SeenMemberDataNonceOrder.Count > 64) SeenMemberDataNonces.Remove(SeenMemberDataNonceOrder.Dequeue());
    }

    [HarmonyPostfix]
    private static void UpdatePostfix(PlayerMovementController __instance)
    {
        // PlayerMovementController.Update runs once per spawned avatar. Keep all
        // BlueSage maintenance on the locally-owned controller so large lobbies do
        // not multiply the visibility-protocol tick by the remote player count.
        if (__instance != NetworkSingleton<TextChannelManager>.I?.MainMovementController) return;
        PollMemberData();
        HostVisibilityProtocolPatch.Tick();
        PlayerRoleLabelController.Tick();
        if (_ownerTransform == null) return;
        if (!Plugin.TryGetRescueLobby(out CSteamID lobby) || lobby == CSteamID.Nil)
        {
            Stop();
            return;
        }
        string currentOwnerSteam = ((ulong)SteamMatchmaking.GetLobbyOwner(lobby)).ToString();
        bool issuerIsCurrentMember = ulong.TryParse(_followLease.IssuerSteamId, out ulong issuerValue) &&
                                     IsCurrentLobbyMember(lobby, new CSteamID(issuerValue));
        bool issuerIsAuthorized = IsAuthorizedMemberDataSender(
            lobby,
            currentOwnerSteam,
            _followLease.IssuerSteamId);
        bool longHaulIsEnabled = string.Equals(
            SteamMatchmaking.GetLobbyData(lobby, "bluesage_rescue_longhaul"),
            currentOwnerSteam + "|1",
            StringComparison.Ordinal);
        if (!RescueFollowLeasePolicy.IsCurrent(
                _followLease,
                ((ulong)lobby).ToString(),
                currentOwnerSteam,
                issuerIsCurrentMember,
                issuerIsAuthorized,
                longHaulIsEnabled) ||
            !string.Equals(currentOwnerSteam, _authorizedOwnerSteam, StringComparison.Ordinal))
        {
            Stop();
            return;
        }
        if (Time.unscaledTime > _hookUntil || _ownerTransform.gameObject == null || !_ownerTransform.gameObject.activeInHierarchy)
        {
            Stop();
            return;
        }
        TextChannelManager chat = NetworkSingleton<TextChannelManager>.I;
        bool forceOwnerSync = Time.unscaledTime >= _nextHookForceSyncAt;
        if (forceOwnerSync) _nextHookForceSyncAt = Time.unscaledTime + 0.25f;
        ApplyLocalRescuePosition(chat, _ownerTransform.position + Vector3.right * 1.25f, forceOwnerSync);
    }
}
