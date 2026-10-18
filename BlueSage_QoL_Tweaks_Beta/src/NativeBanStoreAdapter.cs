using System;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

internal static class NativeBanStoreAdapter
{
    internal static bool TryApplyAdditions(
        CommunityBanAddition[] additions,
        out int existingCount,
        out string error)
    {
        existingCount = 0;
        error = string.Empty;
        DataManager data = MonoSingleton<DataManager>.I;
        BanData bans = data?.BanData;
        if (bans?.BanServerPlayers == null || bans.BanServerPlayerNicks == null)
        {
            error = "native paired storage is unavailable";
            return false;
        }
        if (bans.BanServerPlayers.Count != bans.BanServerPlayerNicks.Count)
        {
            error = "native paired storage is inconsistent";
            return false;
        }

        existingCount = bans.BanServerPlayers.Count;
        int appended = 0;
        try
        {
            CommunityBanAddition[] safeAdditions = additions ?? Array.Empty<CommunityBanAddition>();
            for (int index = 0; index < safeAdditions.Length; index++)
            {
                CommunityBanAddition addition = safeAdditions[index];
                if (addition == null)
                {
                    throw new InvalidOperationException("validated addition was unavailable");
                }
                bans.BanServerPlayers.Add(addition.SteamId);
                bans.BanServerPlayerNicks.Add(addition.Nickname);
                appended++;
            }

            if (appended > 0)
            {
                data.SaveBanData();
            }
            return true;
        }
        catch (Exception ex)
        {
            string rollbackState;
            try
            {
                Rollback(bans, existingCount);
                data.SaveBanData();
                rollbackState = "persisted";
            }
            catch (Exception rollbackEx)
            {
                Rollback(bans, existingCount);
                rollbackState = "memory-only-unresolved-" + rollbackEx.GetType().Name;
            }
            error = ex.GetType().Name +
                    "; rolledBack=" + appended +
                    "; rollback=" + rollbackState;
            return false;
        }
    }

    private static void Rollback(BanData bans, int existingCount)
    {
        while (bans.BanServerPlayers.Count > existingCount)
        {
            bans.BanServerPlayers.RemoveAt(bans.BanServerPlayers.Count - 1);
        }
        while (bans.BanServerPlayerNicks.Count > existingCount)
        {
            bans.BanServerPlayerNicks.RemoveAt(bans.BanServerPlayerNicks.Count - 1);
        }
    }
}
