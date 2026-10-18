using System;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

internal static class PluginShutdownController
{
    private static readonly PluginShutdownPolicy Policy = new PluginShutdownPolicy();

    internal static bool IsShuttingDown => Policy.IsShuttingDown;

    internal static void Begin(
        string trigger,
        Action<SteamRuntimeSnapshot> steamCleanup,
        Action localCleanup)
    {
        if (!Policy.TryBegin(trigger))
        {
            return;
        }

        SteamRuntimeSnapshot snapshot = SteamRuntimeFacade.SnapshotForShutdown();
        if (Policy.TryClaimSteamCleanup())
        {
            try
            {
                steamCleanup?.Invoke(snapshot);
            }
            finally
            {
                Policy.FinishSteamCleanup();
            }
        }

        if (Policy.TryClaimLocalCleanup())
        {
            try
            {
                WorkRelaxRuntimeController.Flush();
                localCleanup?.Invoke();
            }
            finally
            {
                Policy.FinishLocalCleanup();
            }
        }
    }
}
