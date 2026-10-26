using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

// Round 4 WS1: the avatar slot 4-9 store. Plain text, one value per line, names in base64 so any character
// is safe. The last line names the slot count; a file without it (a write cut off by a crash) is rejected.
// v2 (review R2) adds a "legacy=" line: the content hash of the old .cfg as last imported. Empty means the .cfg
// was absent when recorded. v1 files (no such line) still load and report a null hash.
internal static class AvatarSlotStoreFormat
{
    internal const string Header = "BSAVSTORE2";
    internal const string LegacyHeader = "BSAVSTORE1";

    internal static string Format(int selectedSlot, IReadOnlyList<string> names, IReadOnlyList<string> payloads)
    {
        return Format(selectedSlot, null, names, payloads);
    }

    internal static string Format(int selectedSlot, string legacyConfigHash, IReadOnlyList<string> names, IReadOnlyList<string> payloads)
    {
        int count = Math.Min(names.Count, payloads.Count);
        StringBuilder text = new StringBuilder();
        text.Append(Header).Append('\n');
        text.Append("selected=").Append(selectedSlot.ToString(CultureInfo.InvariantCulture)).Append('\n');
        text.Append("legacy=").Append(legacyConfigHash ?? string.Empty).Append('\n');
        for (int index = 0; index < count; index++)
        {
            text.Append("name=").Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(names[index] ?? string.Empty))).Append('\n');
            text.Append("payload=").Append(payloads[index] ?? string.Empty).Append('\n');
        }

        text.Append("end=").Append(count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        return text.ToString();
    }

    internal static bool TryParse(string text, int slotCount, out int selectedSlot, out string[] names, out string[] payloads)
    {
        return TryParse(text, slotCount, out selectedSlot, out _, out names, out payloads);
    }

    internal static bool TryParse(string text, int slotCount, out int selectedSlot, out string legacyConfigHash, out string[] names, out string[] payloads)
    {
        selectedSlot = -1;
        legacyConfigHash = null;
        names = new string[slotCount];
        payloads = new string[slotCount];
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        if (lines.Length == 0)
        {
            return false;
        }

        int firstSlotLine;
        if (lines[0] == Header)
        {
            firstSlotLine = 3;
        }
        else if (lines[0] == LegacyHeader)
        {
            firstSlotLine = 2;
        }
        else
        {
            return false;
        }

        int endLine = firstSlotLine + (slotCount * 2);
        if (lines.Length <= endLine)
        {
            return false;
        }

        if (!lines[endLine].StartsWith("end=", StringComparison.Ordinal) ||
            !int.TryParse(lines[endLine].Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int end) ||
            end != slotCount)
        {
            return false;
        }

        if (!lines[1].StartsWith("selected=", StringComparison.Ordinal) ||
            !int.TryParse(lines[1].Substring(9), NumberStyles.Integer, CultureInfo.InvariantCulture, out selectedSlot))
        {
            return false;
        }

        if (firstSlotLine == 3)
        {
            if (!lines[2].StartsWith("legacy=", StringComparison.Ordinal))
            {
                return false;
            }

            legacyConfigHash = lines[2].Substring(7);
        }

        try
        {
            for (int index = 0; index < slotCount; index++)
            {
                string nameLine = lines[firstSlotLine + (index * 2)];
                string payloadLine = lines[firstSlotLine + 1 + (index * 2)];
                if (!nameLine.StartsWith("name=", StringComparison.Ordinal) || !payloadLine.StartsWith("payload=", StringComparison.Ordinal))
                {
                    return false;
                }

                names[index] = Encoding.UTF8.GetString(Convert.FromBase64String(nameLine.Substring(5)));
                payloads[index] = payloadLine.Substring(8);
            }
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }

    // Candidates in trust order: main file, then backup 1 (newest) to backup N (oldest). Null = missing file.
    internal static int ChooseFirstValid(IReadOnlyList<string> candidatesInOrder, int slotCount)
    {
        for (int index = 0; index < candidatesInOrder.Count; index++)
        {
            if (candidatesInOrder[index] != null && TryParse(candidatesInOrder[index], slotCount, out _, out _, out _))
            {
                return index;
            }
        }

        return -1;
    }

    // Moves to perform before the new main file is swapped in: oldest backup is overwritten first.
    // Key = move from, Value = move to. (KeyValuePair, not a tuple: the plugin targets net46.)
    internal static KeyValuePair<string, string>[] RotationPlan(IReadOnlyList<string> filesInOrder)
    {
        List<KeyValuePair<string, string>> plan = new List<KeyValuePair<string, string>>();
        for (int index = filesInOrder.Count - 2; index >= 0; index--)
        {
            plan.Add(new KeyValuePair<string, string>(filesInOrder[index], filesInOrder[index + 1]));
        }

        return plan.ToArray();
    }
}
