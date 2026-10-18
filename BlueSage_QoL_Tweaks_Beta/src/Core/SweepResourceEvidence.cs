using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

#nullable enable

namespace BlueSage.QoLTweaks.Core;

internal sealed class SweepResourceCategoryTotal
{
    public SweepResourceCategoryTotal(string category, int count, long runtimeBytes)
    {
        Category = category ?? string.Empty;
        Count = count;
        RuntimeBytes = runtimeBytes;
    }

    public string Category { get; }
    public int Count { get; }
    public long RuntimeBytes { get; }
}

internal sealed class SweepResourceAssetFact
{
    public SweepResourceAssetFact(int instanceId, string category, long runtimeBytes)
    {
        InstanceId = instanceId;
        Category = category ?? string.Empty;
        RuntimeBytes = runtimeBytes;
    }

    public int InstanceId { get; }
    public string Category { get; }
    public long RuntimeBytes { get; }
}

internal sealed class SweepResourceSnapshot
{
    public bool Captured { get; set; }
    public string Status { get; set; } = "not-captured";
    public int SceneObjectCount { get; set; }
    public IReadOnlyList<SweepResourceCategoryTotal> Categories { get; set; } = Array.Empty<SweepResourceCategoryTotal>();
    public IReadOnlyList<SweepResourceAssetFact> Assets { get; set; } = Array.Empty<SweepResourceAssetFact>();
    public bool DetailTruncated { get; set; }

    public static SweepResourceSnapshot Unavailable(string exceptionType)
    {
        string safeType = string.IsNullOrWhiteSpace(exceptionType) ? "Unknown" : exceptionType.Trim();
        return new SweepResourceSnapshot { Captured = false, Status = "unavailable:" + safeType };
    }
}

internal static class SweepResourceEvidenceFormatter
{
    private const int MaximumReportedReclaimedAssets = 5;

    public static string Format(SweepResourceSnapshot? before, SweepResourceSnapshot? after)
    {
        if (before == null || !before.Captured)
        {
            return "resourceStatus=" + NormalizeUnavailableStatus(before?.Status);
        }

        if (after == null || !after.Captured)
        {
            return "resourceStatus=" + NormalizeUnavailableStatus(after?.Status);
        }

        Dictionary<string, SweepResourceCategoryTotal> beforeByCategory = ToCategoryMap(before.Categories);
        Dictionary<string, SweepResourceCategoryTotal> afterByCategory = ToCategoryMap(after.Categories);
        string[] categories = beforeByCategory.Keys
            .Concat(afterByCategory.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(CategorySortOrder)
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();

        string resourceText = categories.Length == 0
            ? "none"
            : string.Join("|", categories.Select(category => FormatCategory(
                category,
                GetTotal(beforeByCategory, category),
                GetTotal(afterByCategory, category))));

        string dominantCategory = "none";
        long dominantBytes = 0L;
        foreach (string category in categories)
        {
            long reclaimedBytes = GetTotal(beforeByCategory, category).RuntimeBytes - GetTotal(afterByCategory, category).RuntimeBytes;
            if (reclaimedBytes > dominantBytes)
            {
                dominantBytes = reclaimedBytes;
                dominantCategory = category + "/" + FormatMiB(reclaimedBytes) + "MiB";
            }
        }

        HashSet<int> survivingIds = new HashSet<int>((after.Assets ?? Array.Empty<SweepResourceAssetFact>()).Select(asset => asset.InstanceId));
        SweepResourceAssetFact[] reclaimed = (before.Assets ?? Array.Empty<SweepResourceAssetFact>())
            .Where(asset => asset != null && !survivingIds.Contains(asset.InstanceId))
            .OrderByDescending(asset => asset.RuntimeBytes)
            .ThenBy(asset => asset.Category, StringComparer.Ordinal)
            .ThenBy(asset => asset.InstanceId)
            .Take(MaximumReportedReclaimedAssets)
            .ToArray();
        string reclaimedText = reclaimed.Length == 0
            ? "none"
            : string.Join("|", reclaimed.Select(asset =>
                SanitizeToken(asset.Category, 24) + ":" +
                FormatMiB(asset.RuntimeBytes) + "MiB"));

        int sceneDelta = after.SceneObjectCount - before.SceneObjectCount;
        return "resourceStatus=ok" +
               "; sceneObjects=" + before.SceneObjectCount + "->" + after.SceneObjectCount + "(" + FormatSignedInt(sceneDelta) + ")" +
               "; resources=" + resourceText +
               "; dominantReclaimed=" + dominantCategory +
               "; largestReclaimed=" + reclaimedText +
               "; detailTruncated=" + (before.DetailTruncated || after.DetailTruncated ? "true" : "false");
    }

    private static Dictionary<string, SweepResourceCategoryTotal> ToCategoryMap(IReadOnlyList<SweepResourceCategoryTotal> totals)
    {
        Dictionary<string, SweepResourceCategoryTotal> result = new Dictionary<string, SweepResourceCategoryTotal>(StringComparer.Ordinal);
        if (totals == null) return result;
        foreach (SweepResourceCategoryTotal total in totals)
        {
            if (total == null || string.IsNullOrWhiteSpace(total.Category)) continue;
            result[total.Category] = total;
        }
        return result;
    }

    private static SweepResourceCategoryTotal GetTotal(Dictionary<string, SweepResourceCategoryTotal> totals, string category)
    {
        if (totals.TryGetValue(category, out SweepResourceCategoryTotal? total) && total != null)
        {
            return total;
        }

        return new SweepResourceCategoryTotal(category, 0, 0L);
    }

    private static string FormatCategory(string category, SweepResourceCategoryTotal before, SweepResourceCategoryTotal after)
    {
        return SanitizeToken(category, 24) + "=" +
               before.Count + "/" + FormatMiB(before.RuntimeBytes) + "MiB->" +
               after.Count + "/" + FormatMiB(after.RuntimeBytes) + "MiB" +
               "(delta=" + FormatSignedInt(after.Count - before.Count) + "/" + FormatSignedMiB(after.RuntimeBytes - before.RuntimeBytes) + "MiB)";
    }

    private static string NormalizeUnavailableStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "unavailable:Unknown";
        string safeStatus = status!;
        string exceptionType = safeStatus.StartsWith("unavailable:", StringComparison.Ordinal)
            ? safeStatus.Substring("unavailable:".Length)
            : safeStatus;
        return "unavailable:" + SanitizeToken(exceptionType, 80);
    }

    private static string SanitizeToken(string value, int maximumCharacters)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(unnamed)";
        StringBuilder builder = new StringBuilder(Math.Min(value.Length, maximumCharacters));
        foreach (char character in value.Trim())
        {
            if (builder.Length >= maximumCharacters) break;
            if (char.IsControl(character))
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != ' ') builder.Append(' ');
                continue;
            }
            builder.Append(character == ';' || character == '|' || character == ':' ? '_' : character);
        }
        string result = builder.ToString().Trim();
        return result.Length == 0 ? "(unnamed)" : result;
    }

    private static int CategorySortOrder(string category)
    {
        switch (category)
        {
            case "Texture2D": return 0;
            case "RenderTexture": return 1;
            case "Material": return 2;
            case "Mesh": return 3;
            case "AudioClip": return 4;
            default: return 100;
        }
    }

    private static string FormatSignedInt(int value)
    {
        return value >= 0 ? "+" + value : value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatSignedMiB(long bytes)
    {
        double value = bytes / 1048576d;
        return value >= 0d ? "+" + value.ToString("0.0", CultureInfo.InvariantCulture) : value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string FormatMiB(long bytes)
    {
        return (bytes / 1048576d).ToString("0.0", CultureInfo.InvariantCulture);
    }
}
