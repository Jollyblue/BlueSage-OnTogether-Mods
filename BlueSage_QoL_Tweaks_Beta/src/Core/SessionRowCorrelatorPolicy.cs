using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal static class SessionRowCorrelatorPolicy
{
    internal static bool TryCreate(
        string sessionScope,
        string steamId,
        string playerId,
        int rowIndex,
        out string correlator)
    {
        correlator = string.Empty;
        if (!IsBoundedField(sessionScope, 128) ||
            !IsSteamId64(steamId) ||
            !IsBoundedField(playerId, 128) ||
            rowIndex < 0)
        {
            return false;
        }

        string material =
            sessionScope + "\n" +
            steamId + "\n" +
            playerId + "\n" +
            rowIndex.ToString(CultureInfo.InvariantCulture);
        byte[] bytes = Encoding.UTF8.GetBytes(material);
        ulong hash = 14695981039346656037UL;
        for (int index = 0; index < bytes.Length; index++)
        {
            hash ^= bytes[index];
            hash *= 1099511628211UL;
        }

        correlator =
            "row-" +
            hash.ToString("x16", CultureInfo.InvariantCulture)
                .Substring(0, 12);
        return true;
    }

    private static bool IsSteamId64(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 17)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] < '0' || value[index] > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBoundedField(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsControl(value[index]))
            {
                return false;
            }
        }

        return true;
    }
}
