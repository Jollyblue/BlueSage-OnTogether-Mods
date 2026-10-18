using System;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

internal sealed class AuthoritativeChatSenderContext
{
    internal string SteamId = string.Empty;
    internal string PlayerId = string.Empty;
    internal int NativeRowIndex = -1;
    internal PlayerItemController Row;
    internal NetworkTransform NetworkTransform;
    internal string SessionRowCorrelator = string.Empty;
    internal string RosterNameRaw = string.Empty;
    internal string SteamPersona = string.Empty;
    internal string Role = string.Empty;

    internal bool ExactTupleCaptured =>
        SteamId.Length == 17 &&
        !string.IsNullOrWhiteSpace(PlayerId) &&
        NativeRowIndex >= 0 &&
        Row != null &&
        NetworkTransform != null &&
        !string.IsNullOrWhiteSpace(SessionRowCorrelator);
}

[HarmonyPatch(typeof(TextChannelManager), "SendMessageAsync_Original_0",
    new[] { typeof(byte[]), typeof(byte[]), typeof(bool), typeof(Vector3), typeof(string), typeof(RPCInfo) })]
internal static class ChatSenderContextPatch
{
    // The game passes UTF-16 bytes here. Keep the capture bounded to the same
    // public 4,000-character chat contract, but capture every player message
    // instead of granting special treatment only to @lobby text.
    private const int MaximumChatPayloadBytes =
        InputFieldLimitPolicy.AbsoluteMaxChatCharacters * sizeof(char);
    private static bool _captureFailureLogged;
    private static ulong _sessionLobby;
    private static ulong _sessionOwner;
    private static PlayerPanelController _sessionPanel;
    private static string _sessionScope = string.Empty;

    [ThreadStatic]
    private static AuthoritativeChatSenderContext _currentSenderContext;

    internal static AuthoritativeChatSenderContext CurrentSenderContext =>
        _currentSenderContext;

    internal static string CurrentSenderSteamId =>
        _currentSenderContext?.SteamId ?? string.Empty;

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(
        byte[] __0,
        RPCInfo info,
        out AuthoritativeChatSenderContext __state)
    {
        __state = _currentSenderContext;
        _currentSenderContext = null;
        if (__0 == null ||
            __0.Length == 0 ||
            __0.Length > MaximumChatPayloadBytes ||
            (__0.Length & 1) != 0)
        {
            return;
        }

        _currentSenderContext =
            TryCaptureAuthoritativeSender(info, out AuthoritativeChatSenderContext sender)
                ? sender
                : null;
    }

    [HarmonyFinalizer, HarmonyPriority(Priority.Last)]
    private static Exception Finalizer(
        Exception __exception,
        AuthoritativeChatSenderContext __state)
    {
        _currentSenderContext = __state;
        return __exception;
    }

    private static bool TryCaptureAuthoritativeSender(
        RPCInfo info,
        out AuthoritativeChatSenderContext sender)
    {
        sender = null;
        try
        {
            PlayerPanelController panel =
                NetworkSingleton<PlayerPanelController>.I;
            if (info.manager == null ||
                panel?.PlayerSteamIDs == null ||
                panel.PlayerIDs == null ||
                panel.PlayerTransforms == null ||
                panel.PlayerItemControllers == null ||
                !AuthoritativeChatSenderPolicy.TryResolveUniqueIndex(
                    info.sender,
                    panel.PlayerIDs,
                    out int senderIndex) ||
                senderIndex < 0 ||
                senderIndex >= panel.PlayerSteamIDs.Count ||
                senderIndex >= panel.PlayerIDs.Count ||
                senderIndex >= panel.PlayerTransforms.Count ||
                senderIndex >= panel.PlayerItemControllers.Count ||
                panel.PlayerTransforms[senderIndex] == null ||
                panel.PlayerItemControllers[senderIndex] == null ||
                !PlayerIdentityEvidenceController.TryResolveRosterIndexFresh(
                    senderIndex,
                    out PlayerIdentityEvidence evidence,
                    out _) ||
                !string.Equals(
                    panel.PlayerSteamIDs[senderIndex],
                    evidence.SteamId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    PurrNetPlayerIdPolicy.Format(
                        (ulong)panel.PlayerIDs[senderIndex].id),
                    evidence.NetworkPlayerId,
                    StringComparison.Ordinal) ||
                !TryGetSessionScope(out string sessionScope) ||
                !SessionRowCorrelatorPolicy.TryCreate(
                    sessionScope,
                    evidence.SteamId,
                    evidence.NetworkPlayerId,
                    senderIndex,
                    out string correlator))
            {
                return false;
            }

            sender = new AuthoritativeChatSenderContext
            {
                SteamId = evidence.SteamId,
                PlayerId = evidence.NetworkPlayerId,
                NativeRowIndex = senderIndex,
                Row = panel.PlayerItemControllers[senderIndex],
                NetworkTransform = panel.PlayerTransforms[senderIndex],
                SessionRowCorrelator = correlator,
                RosterNameRaw = evidence.DisplayNameRaw,
                SteamPersona = evidence.SteamPersona,
                Role = evidence.Role,
            };
            return sender.ExactTupleCaptured;
        }
        catch (Exception ex)
        {
            if (!_captureFailureLogged)
            {
                _captureFailureLogged = true;
                Plugin.Log?.LogDebug(
                    "Authoritative chat sender capture failed closed once: " +
                    ex.GetType().Name + ".");
            }

            return false;
        }
    }

    private static bool TryGetSessionScope(out string sessionScope)
    {
        sessionScope = string.Empty;
        if (!SteamManager.Initialized ||
            !Plugin.TryGetRescueLobby(out CSteamID lobby) ||
            lobby == CSteamID.Nil)
        {
            _sessionLobby = 0UL;
            _sessionOwner = 0UL;
            _sessionPanel = null;
            _sessionScope = string.Empty;
            return false;
        }

        ulong currentLobby = (ulong)lobby;
        ulong currentOwner =
            (ulong)SteamMatchmaking.GetLobbyOwner(lobby);
        PlayerPanelController currentPanel =
            NetworkSingleton<PlayerPanelController>.I;
        if (_sessionLobby != currentLobby ||
            _sessionOwner != currentOwner ||
            !ReferenceEquals(_sessionPanel, currentPanel) ||
            string.IsNullOrWhiteSpace(_sessionScope))
        {
            _sessionLobby = currentLobby;
            _sessionOwner = currentOwner;
            _sessionPanel = currentPanel;
            _sessionScope = Guid.NewGuid().ToString("N");
        }

        sessionScope = _sessionScope;
        return true;
    }
}
