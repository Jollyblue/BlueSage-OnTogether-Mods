using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal static class RescueReceiverCapabilityPolicy
{
    internal const string MemberDataKey = "bluesage_qol_rescue_receiver_v1";
    internal const int PublishIntervalSeconds = 5;
    internal const int MaximumAgeSeconds = 15;
    private const int MaximumFutureSkewSeconds = 2;
    private const string Prefix = "v1|";

    internal static string BuildPayload(string pluginVersion, long issuedUnixSeconds)
    {
        string version = (pluginVersion ?? string.Empty).Trim();
        if (version.Length == 0 || version.IndexOf('|') >= 0 || issuedUnixSeconds <= 0)
        {
            return string.Empty;
        }

        return Prefix + version + "|" + issuedUnixSeconds.ToString(CultureInfo.InvariantCulture);
    }

    internal static bool IsFresh(string payload, string expectedPluginVersion, long nowUnixSeconds)
    {
        if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = payload.Substring(Prefix.Length).Split('|');
        if (parts.Length != 2 ||
            !string.Equals(parts[0], expectedPluginVersion, StringComparison.Ordinal) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long issuedUnixSeconds))
        {
            return false;
        }

        long age = nowUnixSeconds - issuedUnixSeconds;
        return age >= -MaximumFutureSkewSeconds && age <= MaximumAgeSeconds;
    }

    internal static bool IsFreshCompatible(string payload, long nowUnixSeconds)
    {
        if (string.IsNullOrWhiteSpace(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string[] parts = payload.Substring(Prefix.Length).Split('|');
        if (parts.Length != 2 ||
            !IsVersionAtLeast020(parts[0]) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long issuedUnixSeconds))
        {
            return false;
        }

        long age = nowUnixSeconds - issuedUnixSeconds;
        return age >= -MaximumFutureSkewSeconds && age <= MaximumAgeSeconds;
    }

    private static bool IsVersionAtLeast020(string value)
    {
        string[] parts = (value ?? string.Empty).Split('.');
        return parts.Length == 3 &&
               int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major) &&
               int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor) &&
               int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int patch) &&
               major >= 0 && minor >= 0 && patch >= 0 &&
               (major > 0 || minor >= 2);
    }
}
