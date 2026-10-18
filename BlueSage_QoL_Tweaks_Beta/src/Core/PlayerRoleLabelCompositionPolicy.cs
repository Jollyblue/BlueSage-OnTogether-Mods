using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal enum PlayerRoleLabelKind
{
    None,
    Host,
    Helper
}

internal readonly struct PlayerRoleLabelCompositionResult
{
    internal PlayerRoleLabelCompositionResult(
        PlayerRoleLabelKind role,
        string renderedText,
        bool applied)
    {
        Role = role;
        RenderedText = renderedText ?? string.Empty;
        Applied = applied;
    }

    internal PlayerRoleLabelKind Role { get; }
    internal string RenderedText { get; }
    internal bool Applied { get; }
}

internal static class PlayerRoleLabelCompositionPolicy
{
    internal static PlayerRoleLabelCompositionResult Compose(
        string nativeText,
        string localizedHostSuffix,
        string hostLabel,
        string helperLabel,
        string rowSteamId,
        string ownerSteamId,
        bool rowIsHost,
        bool uniqueRow,
        bool capabilityFresh,
        IEnumerable<string> ownerHelpers)
    {
        string baseline = nativeText ?? string.Empty;
        if (!uniqueRow || string.IsNullOrWhiteSpace(rowSteamId) || string.IsNullOrWhiteSpace(ownerSteamId))
        {
            return None(baseline);
        }

        if (rowIsHost)
        {
            if (!string.Equals(rowSteamId, ownerSteamId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(localizedHostSuffix) ||
                string.IsNullOrWhiteSpace(hostLabel))
            {
                return None(baseline);
            }

            string decoratedSuffix = " " + hostLabel;
            if (baseline.EndsWith(decoratedSuffix, StringComparison.Ordinal))
            {
                return new PlayerRoleLabelCompositionResult(PlayerRoleLabelKind.Host, baseline, true);
            }

            string nativeSuffix = " " + localizedHostSuffix;
            if (!baseline.EndsWith(nativeSuffix, StringComparison.Ordinal))
            {
                return None(baseline);
            }

            return new PlayerRoleLabelCompositionResult(
                PlayerRoleLabelKind.Host,
                baseline.Substring(0, baseline.Length - nativeSuffix.Length) + decoratedSuffix,
                true);
        }

        if (string.Equals(rowSteamId, ownerSteamId, StringComparison.Ordinal) ||
            !capabilityFresh ||
            string.IsNullOrWhiteSpace(helperLabel) ||
            !ContainsExact(ownerHelpers, rowSteamId))
        {
            return None(baseline);
        }

        string helperSuffix = " " + helperLabel;
        return new PlayerRoleLabelCompositionResult(
            PlayerRoleLabelKind.Helper,
            baseline.EndsWith(helperSuffix, StringComparison.Ordinal)
                ? baseline
                : baseline + helperSuffix,
            true);
    }

    internal static bool TryRestore(
        string currentText,
        string appliedText,
        string nativeText,
        out string restoredText)
    {
        string current = currentText ?? string.Empty;
        restoredText = current;
        if (!string.Equals(current, appliedText ?? string.Empty, StringComparison.Ordinal))
        {
            return false;
        }

        restoredText = nativeText ?? string.Empty;
        return true;
    }

    private static PlayerRoleLabelCompositionResult None(string text)
    {
        return new PlayerRoleLabelCompositionResult(PlayerRoleLabelKind.None, text, false);
    }

    private static bool ContainsExact(IEnumerable<string> values, string expected)
    {
        if (values == null)
        {
            return false;
        }

        foreach (string value in values)
        {
            if (string.Equals(value, expected, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
