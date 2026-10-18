using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal sealed class CommunityBanSyncStatus
{
    internal bool Enabled { get; set; }
    internal DateTime? LastAttemptUtc { get; set; }
    internal DateTime? LastSuccessUtc { get; set; }
    internal string HttpResult { get; set; } = "not attempted";
    internal string SourceRevision { get; set; } = "unavailable";
    internal string SourceHash { get; set; } = "unavailable";
    internal int AcceptedCount { get; set; }
    internal DateTime? NextAttemptUtc { get; set; }
    internal string BoundedError { get; set; } = string.Empty;
}

internal static class CommunityBanSyncPolicy
{
    internal const int MaxStatusFieldCharacters = 128;
    internal const int MaxErrorCharacters = 192;
    internal const int SuccessIntervalSeconds = 21600;
    internal const int RetryIntervalSeconds = 900;

    internal static void BeginAttempt(
        CommunityBanSyncStatus status,
        bool enabled,
        DateTime attemptUtc)
    {
        if (status == null)
        {
            throw new ArgumentNullException(nameof(status));
        }

        status.Enabled = enabled;
        status.LastAttemptUtc = EnsureUtc(attemptUtc);
        status.HttpResult = "not attempted";
        status.SourceRevision = "unavailable";
        status.SourceHash = "unavailable";
        status.AcceptedCount = 0;
        status.NextAttemptUtc = null;
        status.BoundedError = string.Empty;
    }

    internal static string BoundStatusField(string value, string fallback = "unavailable")
    {
        string normalized = Normalize(value, MaxStatusFieldCharacters);
        return normalized.Length == 0 ? fallback : normalized;
    }

    internal static string BoundError(string value)
    {
        return Normalize(value, MaxErrorCharacters);
    }

    internal static DateTime ComputeNextAttemptUtc(DateTime nowUtc, bool succeeded)
    {
        return EnsureUtc(nowUtc).AddSeconds(
            succeeded
                ? SuccessIntervalSeconds
                : RetryIntervalSeconds);
    }

    internal static string BuildStatusText(CommunityBanSyncStatus status)
    {
        CommunityBanSyncStatus safe = status ?? new CommunityBanSyncStatus();
        return "Community BanData: enabled=" + (safe.Enabled ? "yes" : "no") +
               ", lastAttempt=" + FormatUtc(safe.LastAttemptUtc) +
               ", lastSuccess=" + FormatUtc(safe.LastSuccessUtc) +
               ", http=" + BoundStatusField(safe.HttpResult, "not attempted") +
               ", revision=" + BoundStatusField(safe.SourceRevision) +
               ", sha256=" + NormalizeHash(safe.SourceHash) +
               ", accepted=" + Math.Max(0, safe.AcceptedCount).ToString(CultureInfo.InvariantCulture) +
               ", nextAttempt=" + FormatUtc(safe.NextAttemptUtc) +
               ", error=" + (BoundError(safe.BoundedError).Length == 0 ? "none" : BoundError(safe.BoundedError)) +
               ". Additive local validation only; refresh never kicks, bans, or grants authority.";
    }

    internal static string BuildFriendlyStatusText(CommunityBanSyncStatus status)
    {
        CommunityBanSyncStatus safe = status ?? new CommunityBanSyncStatus();
        if (!safe.Enabled)
        {
            return "Community safety list is off.";
        }

        if (BoundError(safe.BoundedError).Length > 0)
        {
            return "Community safety list could not refresh safely. Your existing local list was left unchanged.";
        }

        if (safe.LastSuccessUtc.HasValue)
        {
            return "Community safety list ready: " +
                   Math.Max(0, safe.AcceptedCount).ToString(CultureInfo.InvariantCulture) +
                   " trusted entries checked locally. No player action was taken.";
        }

        return "Community safety list is on and will check locally when ready.";
    }

    internal static string BuildAttestationJson(CommunityBanSyncStatus status)
    {
        CommunityBanSyncStatus safe = status ?? new CommunityBanSyncStatus();
        StringBuilder json = new StringBuilder(512);
        json.Append("{\n");
        AppendJson(json, "schema", "bluesage-community-ban-attestation-v1", true);
        AppendJson(json, "enabled", safe.Enabled ? "true" : "false", true, raw: true);
        AppendJson(json, "lastAttemptUtc", FormatUtc(safe.LastAttemptUtc), true);
        AppendJson(json, "lastSuccessUtc", FormatUtc(safe.LastSuccessUtc), true);
        AppendJson(json, "httpResult", BoundStatusField(safe.HttpResult, "not attempted"), true);
        AppendJson(json, "sourceRevision", BoundStatusField(safe.SourceRevision), true);
        AppendJson(json, "sourceSha256", NormalizeHash(safe.SourceHash), true);
        AppendJson(json, "acceptedCount", Math.Max(0, safe.AcceptedCount).ToString(CultureInfo.InvariantCulture), true, raw: true);
        AppendJson(json, "nextAttemptUtc", FormatUtc(safe.NextAttemptUtc), true);
        AppendJson(json, "error", BoundError(safe.BoundedError), false);
        json.Append("}\n");
        return json.ToString();
    }

    private static void AppendJson(
        StringBuilder target,
        string key,
        string value,
        bool comma,
        bool raw = false)
    {
        target.Append("  \"").Append(key).Append("\": ");
        if (raw)
        {
            target.Append(value);
        }
        else
        {
            target.Append('"').Append(EscapeJson(value)).Append('"');
        }
        target.Append(comma ? ",\n" : "\n");
    }

    private static string FormatUtc(DateTime? value)
    {
        return value.HasValue
            ? EnsureUtc(value.Value).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : "never";
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static string NormalizeHash(string value)
    {
        string candidate = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (candidate.Length != 64) return "unavailable";
        for (int index = 0; index < candidate.Length; index++)
        {
            char character = candidate[index];
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'A' && character <= 'F')))
            {
                return "unavailable";
            }
        }
        return candidate;
    }

    private static string Normalize(string value, int limit)
    {
        StringBuilder clean = new StringBuilder();
        string source = (value ?? string.Empty).Trim();
        bool priorWhitespace = false;
        for (int index = 0; index < source.Length && clean.Length < limit; index++)
        {
            char character = source[index];
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                if (clean.Length > 0 && !priorWhitespace)
                {
                    clean.Append(' ');
                    priorWhitespace = true;
                }
                continue;
            }
            if (character == '<' || character == '>')
            {
                continue;
            }
            clean.Append(character);
            priorWhitespace = false;
        }
        return clean.ToString().Trim();
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}
