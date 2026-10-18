using System;
using System.Collections.Generic;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch]
internal static class VoicePositionRateGuardPatch
{
    private const int MaximumTrackedObjects = 64;
    private const float StaleStateSeconds = 30f;
    private static readonly Dictionary<int, PositionState> States = new Dictionary<int, PositionState>();
    private static readonly object TargetResolutionSync = new object();
    private static MethodBase _targetMethod;
    private static bool _targetResolutionAttempted;
    private static float _nextPruneAt;
    private static long _forwarded;
    private static long _suppressed;
    private static bool _activationLogged;

    private sealed class PositionState
    {
        internal WeakReference Participant;
        internal float LastForwardedAt = -1f;
        internal float LastSeenAt;
        internal string Channel = string.Empty;
    }

    [HarmonyPrepare]
    private static bool Prepare()
    {
        bool available = TargetMethod() != null;
        if (!available)
        {
            Plugin.Log?.LogWarning("Vivox 3D-position rate guard is unavailable on this game build; the original call path remains untouched.");
        }
        return available;
    }

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        lock (TargetResolutionSync)
        {
            if (_targetResolutionAttempted) return _targetMethod;
            try
            {
                Type vivoxServiceType = AccessTools.TypeByName("Unity.Services.Vivox.VivoxServiceInternal");
                if (vivoxServiceType != null)
                {
                    _targetMethod = AccessTools.Method(
                        vivoxServiceType,
                        "Set3DPosition",
                        new[] { typeof(GameObject), typeof(string), typeof(bool) });
                }
            }
            finally
            {
                _targetResolutionAttempted = true;
            }

            return _targetMethod;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix(GameObject participantObject, string channelName)
    {
        if (Plugin.EnableVivoxPositionRateGuard == null || !Plugin.EnableVivoxPositionRateGuard.Value)
        {
            return true;
        }

        if (participantObject == null || string.IsNullOrWhiteSpace(channelName))
        {
            return true;
        }

        float now = Time.realtimeSinceStartup;
        PruneIfNeeded(now);
        int key = participantObject.GetInstanceID();
        if (!States.TryGetValue(key, out PositionState state) ||
            state.Participant == null ||
            !ReferenceEquals(state.Participant.Target, participantObject))
        {
            if (States.Count >= MaximumTrackedObjects)
            {
                States.Clear();
            }
            state = new PositionState { Participant = new WeakReference(participantObject) };
            States[key] = state;
        }

        state.LastSeenAt = now;
        if (!VoicePositionRatePolicy.ShouldForward(
                now,
                state.LastForwardedAt,
                channelName,
                state.Channel,
                VoicePositionRatePolicy.DefaultMinimumIntervalSeconds))
        {
            _suppressed++;
            if (!_activationLogged)
            {
                _activationLogged = true;
                Plugin.Log?.LogInfo("Vivox 3D-position safety guard active: coalescing the game's per-frame requests to 4 Hz; exact voice identities remain private.");
            }
            return false;
        }

        state.LastForwardedAt = now;
        state.Channel = channelName;
        _forwarded++;
        return true;
    }

    private static void PruneIfNeeded(float now)
    {
        if (now < _nextPruneAt && States.Count < MaximumTrackedObjects)
        {
            return;
        }

        _nextPruneAt = now + 10f;
        var staleKeys = new List<int>();
        foreach (KeyValuePair<int, PositionState> pair in States)
        {
            PositionState state = pair.Value;
            if (state == null || state.Participant == null || !state.Participant.IsAlive || now - state.LastSeenAt > StaleStateSeconds)
            {
                staleKeys.Add(pair.Key);
            }
        }

        foreach (int key in staleKeys)
        {
            States.Remove(key);
        }
    }

    internal static string GetDiagnosticSummary()
    {
        return $"enabled={Plugin.EnableVivoxPositionRateGuard?.Value == true},available={_targetMethod != null},tracked={States.Count},forwarded={_forwarded},suppressed={_suppressed}";
    }
}
