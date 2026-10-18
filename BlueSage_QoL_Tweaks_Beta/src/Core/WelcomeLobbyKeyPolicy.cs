#nullable enable

using System;
using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

// Identity of one "visit" to a lobby for the chat welcome (UAT 861AEF4EFB58, 2026-10-17). Every join
// or rejoin loads a fresh game scene, so keying on the scene instance re-arms the welcome on each
// visit: reconnects to the same lobby and fast lobby-hops included. The Steam lobby id separates
// lobbies that have no join code; the code and visible-lobby fallbacks keep older detection paths.
internal static class WelcomeLobbyKeyPolicy
{
    private const string ScenePrefix = "|scene:";

    internal static string Build(ulong steamLobbyId, int sceneInstanceId, string? lobbyCode, bool hasVisibleLobby)
    {
        return Build(steamLobbyId, sceneInstanceId, lobbyCode, () => hasVisibleLobby);
    }

    // The visible-lobby check costs reflection, so it runs only when no Steam id or code exists
    // (ultra review, 2026-10-17).
    internal static string Build(ulong steamLobbyId, int sceneInstanceId, string? lobbyCode, Func<bool> hasVisibleLobby)
    {
        string scene = ScenePrefix + sceneInstanceId.ToString(CultureInfo.InvariantCulture);
        if (steamLobbyId != 0UL)
        {
            return "steam:" + steamLobbyId.ToString(CultureInfo.InvariantCulture) + scene;
        }

        if (!string.IsNullOrWhiteSpace(lobbyCode))
        {
            return "code:" + lobbyCode!.Trim() + scene;
        }

        return hasVisibleLobby() ? "visible" + scene : string.Empty;
    }

    // Review 2026-10-18: the prefix can change mid-visit (code: or visible first, steam: once the Steam
    // lobby id resolves), but the scene instance can't. Two keys are the same visit when their scene parts match.
    internal static bool IsSameVisit(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
        {
            return false;
        }

        return string.Equals(VisitPart(first!), VisitPart(second!), StringComparison.Ordinal);
    }

    private static string VisitPart(string key)
    {
        int scene = key.IndexOf(ScenePrefix, StringComparison.Ordinal);
        return scene < 0 ? key : key.Substring(scene);
    }

    // The Steam lobby id IS the join code, and players paste LogOutput.log into Discord: log keys
    // only in this form, which keeps the last four characters like RedactLobbyCode.
    internal static string Redact(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "[none]";
        }

        int colon = key!.IndexOf(':');
        int scene = key.IndexOf(ScenePrefix, StringComparison.Ordinal);
        if (colon < 0 || scene < 0 || colon > scene)
        {
            return key;
        }

        string id = key.Substring(colon + 1, scene - colon - 1);
        string redacted = id.Length <= 4 ? "[redacted]" : "[redacted:" + id.Substring(id.Length - 4) + "]";
        return key.Substring(0, colon + 1) + redacted + key.Substring(scene);
    }
}
