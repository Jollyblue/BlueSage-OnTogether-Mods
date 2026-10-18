using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using Steamworks;

namespace BlueSage.QoLTweaks;

internal static class SweepSessionEvidenceCapture
{
    internal static SweepSessionSnapshot Capture()
    {
        try
        {
            long workingSetBytes;
            long privateBytes;
            int handleCount;
            int threadCount;
            using (Process process = Process.GetCurrentProcess())
            {
                process.Refresh();
                workingSetBytes = process.WorkingSet64;
                privateBytes = process.PrivateMemorySize64;
                handleCount = process.HandleCount;
                threadCount = process.Threads.Count;
            }

            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            int nativeRows = panel?.PlayerSteamIDs?.Count ?? -1;

            int steamMembers = -1;
            MultiplayerManager multiplayerManager = MonoSingleton<MultiplayerManager>.I;
            if (SteamManager.Initialized &&
                multiplayerManager != null &&
                ulong.TryParse(multiplayerManager.LobbyCode, out ulong lobbyId) &&
                lobbyId > 0UL)
            {
                steamMembers = SteamMatchmaking.GetNumLobbyMembers(new CSteamID(lobbyId));
            }

            VoiceManager voiceManager = MonoSingleton<VoiceManager>.I;
            return new SweepSessionSnapshot(
                captured: true,
                status: "ok",
                processWorkingSetBytes: workingSetBytes,
                processPrivateBytes: privateBytes,
                processHandleCount: handleCount,
                processThreadCount: threadCount,
                nativeRows: nativeRows,
                steamMembers: steamMembers,
                voiceManagerPresent: voiceManager != null,
                voiceLobbyOff: voiceManager?.IsVoiceLobbyOff ?? false,
                voiceJoined: voiceManager?.IsJoined ?? false,
                queuedVoiceParticipants: CountQueuedVoiceParticipants(voiceManager));
        }
        catch (Exception ex)
        {
            return SweepSessionSnapshot.Unavailable(ex.GetType().Name);
        }
    }

    private static int CountQueuedVoiceParticipants(VoiceManager voiceManager)
    {
        if (voiceManager == null) return -1;
        try
        {
            FieldInfo field = typeof(VoiceManager).GetField(
                "QueuedParticipants",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(voiceManager) is ICollection collection ? collection.Count : -1;
        }
        catch
        {
            return -1;
        }
    }
}
