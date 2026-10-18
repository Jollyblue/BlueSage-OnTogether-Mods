using System;
using System.Collections.Generic;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct ChalkboardCatalogEntry
{
    internal ChalkboardCatalogEntry(int index, string label)
    {
        Index = index;
        Label = label;
    }

    internal int Index { get; }
    internal string Label { get; }
}

internal static class ChalkboardCatalogPolicy
{
    internal const string CatalogRevision = "build-24259292-v1";
    internal const int MaxCatalogIndex = 2;
    private static readonly ChalkboardCatalogEntry[] Entries =
    {
        new ChalkboardCatalogEntry(0, "River/Stage"),
        new ChalkboardCatalogEntry(1, "Classroom board A (face A)"),
        new ChalkboardCatalogEntry(2, "Classroom board B (face B)")
    };

    internal static IReadOnlyList<ChalkboardCatalogEntry> Build24259292 => Entries;

    internal static bool TryGet(int index, out ChalkboardCatalogEntry entry)
    {
        if (index < 0 || index > MaxCatalogIndex)
        {
            entry = default;
            return false;
        }
        entry = Entries[index];
        return true;
    }

    internal static bool TryParseExactIndex(
        string token,
        int liveBoardCount,
        out ChalkboardCatalogEntry entry,
        out string error)
    {
        entry = default;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(token) ||
            !int.TryParse(token.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
        {
            error = "Choose an exact board index 0, 1, or 2 from /chalk boards.";
            return false;
        }
        if (!TryGet(index, out entry))
        {
            error = "Board index " + index + " is outside the verified chalkboard catalog (boards 0-2); index 3 never identifies a supported board.";
            return false;
        }
        if (liveBoardCount <= index)
        {
            error = "Board " + index + " is not available in the current scene. Refresh /chalk boards.";
            return false;
        }
        return true;
    }
}
