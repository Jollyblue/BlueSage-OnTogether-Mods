using UnityEngine;

namespace BlueSage.QoLTweaks.Core;

internal static class EasterEggTracker
{
    private const string TotalKey = "BlueSageQoL.EasterEggs.Total";
    private const string Prefix = "BlueSageQoL.EasterEggs.";

    public static int Count => PlayerPrefs.GetInt(TotalKey, 0);

    public static bool HasDiscovered(string id)
    {
        return PlayerPrefs.GetInt(Prefix + id, 0) != 0;
    }

    public static string Discover(string id, string label)
    {
        string key = Prefix + id;
        bool firstTime = PlayerPrefs.GetInt(key, 0) == 0;
        if (firstTime)
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.SetInt(TotalKey, Count + 1);
            PlayerPrefs.Save();
        }

        string prefix = firstTime ? "New BlueSage easter egg found" : "BlueSage easter egg already found";
        return $"{prefix}: {label}. Eggs found: {Count}.";
    }
}
