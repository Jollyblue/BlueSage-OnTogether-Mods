using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BlueSage.QoLTweaks.Core;

internal sealed class DetectedDisplayNameState
{
    public string BaseName { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "FFD45D";
    public bool SpoonsEnabled { get; set; }
    public int SpoonCount { get; set; } = 5;
    public string SpoonLabel { get; set; } = "sp";
    public bool RepairedLegacyMarkup { get; set; }
    public bool UsedPlainTextFallback { get; set; }
    public string DriftSummary { get; set; } = string.Empty;
}

internal static class StyleHelperNamePolicy
{
    private static readonly Regex TrailingColorWrapperPattern = new Regex(
        @"^(?<base>.*)\s(?:<\s*#(?<short>[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\s*>|<\s*color\s*=\s*#?(?<long>[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})\s*>)(?<text>.*)</\s*color\s*>\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex SpoonTextPattern = new Regex(
        @"^\[(?<count>[0-5])/5(?<label>[A-Za-z0-9_-]{1,12})\]$",
        RegexOptions.Compiled);

    public static string ToPlainBaseNameForDetectCurrent(
        string liveDisplayName,
        string staleSavedBaseName,
        string knownStatusSuffix = "",
        string knownSpoonSuffix = "")
    {
        string live = RichTextStyleBuilder.ToPlainStyleEditorText(
            StripKnownSuffixesPreservingRichText(liveDisplayName, knownStatusSuffix, knownSpoonSuffix));
        if (!string.IsNullOrWhiteSpace(live))
        {
            return live.Trim();
        }

        return RichTextStyleBuilder.ToPlainStyleEditorText(staleSavedBaseName).Trim();
    }

    public static string ToStyledBaseNameForDetectCurrent(
        string liveDisplayName,
        string staleSavedBaseName,
        string knownStatusSuffix = "",
        string knownSpoonSuffix = "")
    {
        string live = StripKnownSuffixesPreservingRichText(liveDisplayName, knownStatusSuffix, knownSpoonSuffix);
        RichTextRepairResult liveRepair = RichTextDraftPolicy.RepairLegacy(live);
        live = liveRepair.Value;
        if (!string.IsNullOrWhiteSpace(RichTextStyleBuilder.ToPlainStyleEditorText(live)))
        {
            return live;
        }

        return RichTextDraftPolicy.RepairLegacy(staleSavedBaseName).Value;
    }

    public static DetectedDisplayNameState DetectCurrentState(
        string liveDisplayName,
        string savedBaseName,
        string savedStatusMessage,
        string savedStatusColor,
        string savedStatusBrackets,
        bool savedSpoonsEnabled,
        int savedSpoonCount,
        string savedSpoonLabel,
        string knownStatusSuffix,
        string knownSpoonSuffix)
    {
        string live = (liveDisplayName ?? string.Empty).Trim();
        string savedBase = (savedBaseName ?? string.Empty).Trim();
        string normalizedStatusColor = NormalizeHex(savedStatusColor, "FFD45D");
        string normalizedSpoonLabel = NormalizeSpoonLabel(savedSpoonLabel);
        bool hasLive = !string.IsNullOrWhiteSpace(live);
        string working = hasLive ? live : savedBase;

        bool detectedSpoons = false;
        int detectedSpoonCount = ClampSpoonCount(savedSpoonCount);
        string detectedSpoonLabel = normalizedSpoonLabel;
        if (hasLive && TryRemoveTrailingSpoons(ref working, out int liveSpoonCount, out string liveSpoonLabel))
        {
            detectedSpoons = true;
            detectedSpoonCount = liveSpoonCount;
            detectedSpoonLabel = liveSpoonLabel;
        }

        bool detectedStatus = false;
        string detectedStatusMessage = string.Empty;
        string detectedStatusColor = normalizedStatusColor;
        if (hasLive && TryRemoveTrailingStatus(
                ref working,
                savedBase,
                savedStatusMessage,
                normalizedStatusColor,
                savedStatusBrackets,
                out string liveStatusMessage,
                out string liveStatusColor))
        {
            detectedStatus = true;
            detectedStatusMessage = liveStatusMessage;
            detectedStatusColor = liveStatusColor;
        }

        // 0.1.8 could capture one or more generated suffixes into StatusBaseName. Remove only
        // exact known suffixes or the strongly shaped BlueSage status/Spoons wrappers.
        string prior;
        do
        {
            prior = working;
            working = StripKnownSuffixesPreservingRichText(working, knownStatusSuffix, knownSpoonSuffix);
            _ = TryRemoveTrailingSpoons(ref working, out _, out _);
            _ = TryRemoveTrailingStatus(
                ref working,
                savedBase,
                savedStatusMessage,
                normalizedStatusColor,
                savedStatusBrackets,
                out _,
                out _);
        }
        while (!string.Equals(prior, working, StringComparison.Ordinal));

        RichTextRepairResult repairedBase = RichTextDraftPolicy.RepairLegacy(working);
        string baseName = repairedBase.Value;
        if (string.IsNullOrWhiteSpace(RichTextStyleBuilder.ToPlainStyleEditorText(baseName)))
        {
            RichTextRepairResult savedRepair = RichTextDraftPolicy.RepairLegacy(savedBase);
            baseName = savedRepair.Value;
            repairedBase = savedRepair;
        }

        var drift = new List<string>();
        if (hasLive && !string.Equals(baseName, savedBase, StringComparison.Ordinal))
        {
            drift.Add("live base differed from saved state");
        }
        string effectiveStatus = hasLive ? (detectedStatus ? detectedStatusMessage : string.Empty) : (savedStatusMessage ?? string.Empty).Trim();
        string effectiveStatusColor = detectedStatus ? detectedStatusColor : normalizedStatusColor;
        if (hasLive && !string.Equals(effectiveStatus, (savedStatusMessage ?? string.Empty).Trim(), StringComparison.Ordinal))
        {
            drift.Add("live status differed from saved state");
        }
        bool effectiveSpoons = hasLive ? detectedSpoons : savedSpoonsEnabled;
        int effectiveSpoonCount = detectedSpoons ? detectedSpoonCount : ClampSpoonCount(savedSpoonCount);
        string effectiveSpoonLabel = detectedSpoons ? detectedSpoonLabel : normalizedSpoonLabel;
        if (hasLive && (effectiveSpoons != savedSpoonsEnabled ||
            (effectiveSpoons && (effectiveSpoonCount != ClampSpoonCount(savedSpoonCount) ||
             !string.Equals(effectiveSpoonLabel, normalizedSpoonLabel, StringComparison.Ordinal)))))
        {
            drift.Add("live Spoons differed from saved state");
        }
        if (repairedBase.Changed)
        {
            drift.Add(repairedBase.UsedPlainTextFallback
                ? "legacy rich text fell back to visible text"
                : "legacy rich text was balanced");
        }

        return new DetectedDisplayNameState
        {
            BaseName = baseName,
            StatusMessage = effectiveStatus,
            StatusColor = effectiveStatusColor,
            SpoonsEnabled = effectiveSpoons,
            SpoonCount = effectiveSpoonCount,
            SpoonLabel = effectiveSpoonLabel,
            RepairedLegacyMarkup = repairedBase.Changed,
            UsedPlainTextFallback = repairedBase.UsedPlainTextFallback,
            DriftSummary = drift.Count == 0 ? "Live and saved style state agree." : "Reconciled: " + string.Join("; ", drift) + "."
        };
    }

    internal static string StripKnownSuffixesPreservingRichText(
        string displayName,
        string knownStatusSuffix,
        string knownSpoonSuffix)
    {
        string cleaned = (displayName ?? string.Empty).Trim();
        string prior;
        do
        {
            prior = cleaned;
            cleaned = RemoveExactTrailingSuffix(cleaned, knownSpoonSuffix);
            cleaned = RemoveExactTrailingSuffix(cleaned, knownStatusSuffix);
        }
        while (!string.Equals(prior, cleaned, StringComparison.Ordinal));
        return cleaned.Trim();
    }

    private static string RemoveExactTrailingSuffix(string value, string suffix)
    {
        string expected = (suffix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            return value;
        }

        string token = " " + expected;
        return value.EndsWith(token, System.StringComparison.Ordinal)
            ? value.Substring(0, value.Length - token.Length).TrimEnd()
            : value;
    }

    private static bool TryRemoveTrailingSpoons(ref string value, out int count, out string label)
    {
        count = 5;
        label = "sp";
        Match wrapper = TrailingColorWrapperPattern.Match(value ?? string.Empty);
        if (!wrapper.Success || !string.Equals(GetWrapperHex(wrapper), "70FFBD", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Match spoon = SpoonTextPattern.Match(wrapper.Groups["text"].Value);
        if (!spoon.Success || !int.TryParse(spoon.Groups["count"].Value, out count))
        {
            return false;
        }

        label = NormalizeSpoonLabel(spoon.Groups["label"].Value);
        value = wrapper.Groups["base"].Value.TrimEnd();
        return true;
    }

    private static bool TryRemoveTrailingStatus(
        ref string value,
        string savedBaseName,
        string savedStatusMessage,
        string savedStatusColor,
        string savedStatusBrackets,
        out string statusMessage,
        out string statusColor)
    {
        statusMessage = string.Empty;
        statusColor = NormalizeHex(savedStatusColor, "FFD45D");
        Match wrapper = TrailingColorWrapperPattern.Match(value ?? string.Empty);
        if (!wrapper.Success)
        {
            return false;
        }

        string wrapperHex = NormalizeHex(GetWrapperHex(wrapper), string.Empty);
        if (!string.Equals(wrapperHex, statusColor, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string brackets = string.IsNullOrEmpty(savedStatusBrackets) || savedStatusBrackets.Length < 2
            ? "()"
            : savedStatusBrackets;
        string text = wrapper.Groups["text"].Value;
        string prefix = brackets.Substring(0, 1);
        string suffix = brackets.Substring(1, 1);
        if (!text.StartsWith(prefix, StringComparison.Ordinal) ||
            !text.EndsWith(suffix, StringComparison.Ordinal) ||
            text.Length < prefix.Length + suffix.Length)
        {
            return false;
        }

        statusMessage = text.Substring(prefix.Length, text.Length - prefix.Length - suffix.Length);
        if (string.IsNullOrWhiteSpace(savedStatusMessage) &&
            !CanReachSavedBaseByRemovingMatchingStatusWrappers(
                wrapper.Groups["base"].Value,
                savedBaseName,
                statusColor,
                prefix,
                suffix))
        {
            statusMessage = string.Empty;
            return false;
        }

        value = wrapper.Groups["base"].Value.TrimEnd();
        return true;
    }

    private static bool CanReachSavedBaseByRemovingMatchingStatusWrappers(
        string candidateBase,
        string savedBaseName,
        string statusColor,
        string prefix,
        string suffix)
    {
        string expected = RichTextDraftPolicy.RepairLegacy(savedBaseName).Value;
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        string probe = (candidateBase ?? string.Empty).TrimEnd();
        while (!string.IsNullOrWhiteSpace(probe))
        {
            string repairedProbe = RichTextDraftPolicy.RepairLegacy(probe).Value;
            if (string.Equals(repairedProbe, expected, StringComparison.Ordinal))
            {
                return true;
            }

            Match nested = TrailingColorWrapperPattern.Match(probe);
            if (!nested.Success ||
                !string.Equals(NormalizeHex(GetWrapperHex(nested), string.Empty), statusColor, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string nestedText = nested.Groups["text"].Value;
            if (!nestedText.StartsWith(prefix, StringComparison.Ordinal) ||
                !nestedText.EndsWith(suffix, StringComparison.Ordinal) ||
                nestedText.Length < prefix.Length + suffix.Length)
            {
                return false;
            }

            probe = nested.Groups["base"].Value.TrimEnd();
        }

        return false;
    }

    private static string GetWrapperHex(Match wrapper)
    {
        return wrapper.Groups["short"].Success
            ? wrapper.Groups["short"].Value
            : wrapper.Groups["long"].Value;
    }

    private static string NormalizeHex(string value, string fallback)
    {
        string hex = (value ?? string.Empty).Trim().TrimStart('#');
        if (hex.Length == 3)
        {
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        }
        return hex.Length == 6 && Regex.IsMatch(hex, "^[0-9A-Fa-f]{6}$")
            ? hex.ToUpperInvariant()
            : fallback;
    }

    private static int ClampSpoonCount(int value)
    {
        return value < 0 ? 0 : value > 5 ? 5 : value;
    }

    private static string NormalizeSpoonLabel(string value)
    {
        string cleaned = Regex.Replace(value ?? string.Empty, "[^A-Za-z0-9_-]", string.Empty);
        if (cleaned.Length > 12)
        {
            cleaned = cleaned.Substring(0, 12);
        }
        return string.IsNullOrWhiteSpace(cleaned) ? "sp" : cleaned;
    }
}
