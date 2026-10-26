using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum HostJournalRowKind
{
    Action,
    Status
}

// Round 4 Stage D (Blue, 2026-10-19): one host journal. Action rows say who did what to whom, with the role, the
// surface the click came from (ID card, player row, report, command, menu, helper relay), the target's Steam ID and
// name (Steam IDs are public, Blue's ruling), the PurrNet id, the RECONNECTED/LOADING label and the result. Status
// rows say host/guest, lobby valid, Steam count/limit, live roster, transport-verified count and host health.
internal sealed class HostJournalRow
{
    public HostJournalRowKind Kind;
    public long UtcTicks;

    public string Actor = string.Empty;
    public string ActorRole = string.Empty;
    public string Surface = string.Empty;
    public string Action = string.Empty;
    public string TargetSteamId = string.Empty;
    public string TargetName = string.Empty;
    public string TargetPurrNetId = string.Empty;
    public string Label = string.Empty;
    public bool Success;
    public string Result = string.Empty;

    public bool IsHost;
    public bool LobbyValid;
    public int SteamCount;
    public int SteamLimit;
    public int LiveRoster;
    public int TransportVerified;
    public string HostHealth = string.Empty;

    internal static HostJournalRow ForAction(long utcTicks, string actor, string actorRole, string surface, string action, string targetSteamId, string targetName, string targetPurrNetId, string label, bool success, string result)
    {
        return new HostJournalRow
        {
            Kind = HostJournalRowKind.Action,
            UtcTicks = utcTicks,
            Actor = actor ?? string.Empty,
            ActorRole = actorRole ?? string.Empty,
            Surface = surface ?? string.Empty,
            Action = action ?? string.Empty,
            TargetSteamId = targetSteamId ?? string.Empty,
            TargetName = targetName ?? string.Empty,
            TargetPurrNetId = targetPurrNetId ?? string.Empty,
            Label = label ?? string.Empty,
            Success = success,
            Result = result ?? string.Empty,
        };
    }

    internal static HostJournalRow ForStatus(long utcTicks, bool isHost, bool lobbyValid, int steamCount, int steamLimit, int liveRoster, int transportVerified, string hostHealth)
    {
        return new HostJournalRow
        {
            Kind = HostJournalRowKind.Status,
            UtcTicks = utcTicks,
            IsHost = isHost,
            LobbyValid = lobbyValid,
            SteamCount = steamCount,
            SteamLimit = steamLimit,
            LiveRoster = liveRoster,
            TransportVerified = transportVerified,
            HostHealth = hostHealth ?? string.Empty,
        };
    }

    // What the "on change" rule compares: host, lobby valid and health. Round 5d: the player counts left the key;
    // they changed on every join and leave (260 rows in 6 h at 41 players on 10-23) and now ride on the 5-minute
    // heartbeat row, which still carries the latest counts.
    internal string StatusKey()
    {
        return string.Join("|", (IsHost ? "h" : "g"), (LobbyValid ? "v" : "x"), HostHealth);
    }
}

internal static class HostJournalFormat
{
    internal const int Version = 1;
    private const int MaximumFieldCharacters = 240;

    internal static string ToJsonLine(HostJournalRow row)
    {
        StringBuilder json = new StringBuilder(320);
        json.Append("{\"v\":").Append(Version.ToString(CultureInfo.InvariantCulture));
        json.Append(",\"kind\":\"").Append(row.Kind == HostJournalRowKind.Action ? "action" : "status").Append('"');
        json.Append(",\"utc\":\"").Append(new DateTime(row.UtcTicks, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append('"');
        if (row.Kind == HostJournalRowKind.Action)
        {
            Field(json, "actor", row.Actor);
            Field(json, "actorRole", row.ActorRole);
            Field(json, "surface", row.Surface);
            Field(json, "action", row.Action);
            Field(json, "targetSteamId", row.TargetSteamId);
            Field(json, "targetName", row.TargetName);
            Field(json, "targetPurrNetId", row.TargetPurrNetId);
            Field(json, "label", row.Label);
            json.Append(",\"success\":").Append(row.Success ? "true" : "false");
            Field(json, "result", row.Result);
        }
        else
        {
            json.Append(",\"host\":").Append(row.IsHost ? "true" : "false");
            json.Append(",\"lobbyValid\":").Append(row.LobbyValid ? "true" : "false");
            json.Append(",\"steamCount\":").Append(row.SteamCount.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"steamLimit\":").Append(row.SteamLimit.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"liveRoster\":").Append(row.LiveRoster.ToString(CultureInfo.InvariantCulture));
            json.Append(",\"transportVerified\":").Append(row.TransportVerified.ToString(CultureInfo.InvariantCulture));
            Field(json, "hostHealth", row.HostHealth);
        }

        json.Append('}');
        return json.ToString();
    }

    // One short line for the Host Tools scroll box. Local time of day, then the facts a host scans for.
    internal static string ToDisplayLine(HostJournalRow row)
    {
        string clock = new DateTime(row.UtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        if (row.Kind == HostJournalRowKind.Action)
        {
            string outcome = row.Success ? "ok" : "refused";
            string label = string.IsNullOrEmpty(row.Label) ? string.Empty : " [" + row.Label + "]";
            string name = string.IsNullOrEmpty(row.TargetName) ? string.Empty : row.TargetName + " ";
            return clock + " " + row.Action.ToUpperInvariant() + " by " + row.ActorRole + " (" + row.Surface + ") -> " + name + row.TargetSteamId + label + " " + outcome + ": " + row.Result;
        }

        return clock + " status: " + (row.IsHost ? "host" : "guest") + ", lobby " + (row.LobbyValid ? "valid" : "invalid") + ", steam " +
               row.SteamCount.ToString(CultureInfo.InvariantCulture) + "/" + row.SteamLimit.ToString(CultureInfo.InvariantCulture) +
               ", in game " + row.LiveRoster.ToString(CultureInfo.InvariantCulture) + ", matched " + row.TransportVerified.ToString(CultureInfo.InvariantCulture) +
               ", health " + row.HostHealth;
    }

    private static void Field(StringBuilder json, string name, string value)
    {
        json.Append(",\"").Append(name).Append("\":\"").Append(Escape(Limit(value ?? string.Empty))).Append('"');
    }

    private static string Limit(string value)
    {
        return value.Length <= MaximumFieldCharacters ? value : value.Substring(0, MaximumFieldCharacters);
    }

    internal static string Escape(string value)
    {
        StringBuilder result = new StringBuilder(value.Length + 16);
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (character < ' ')
                    {
                        result.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result.Append(character);
                    }

                    break;
            }
        }

        return result.ToString();
    }
}

// The last 50 rows, for the in-game box. Oldest rows fall off; nothing here is ever the record (the file is).
internal sealed class HostJournalRing
{
    private readonly HostJournalRow[] _rows;
    private int _next;
    private int _count;

    internal HostJournalRing(int capacity = 50)
    {
        _rows = new HostJournalRow[Math.Max(1, capacity)];
    }

    internal int Capacity => _rows.Length;
    internal int Count => _count;

    internal void Add(HostJournalRow row)
    {
        _rows[_next] = row;
        _next = (_next + 1) % _rows.Length;
        if (_count < _rows.Length)
        {
            _count++;
        }
    }

    internal IReadOnlyList<HostJournalRow> SnapshotNewestFirst()
    {
        List<HostJournalRow> snapshot = new List<HostJournalRow>(_count);
        for (int offset = 1; offset <= _count; offset++)
        {
            snapshot.Add(_rows[(_next - offset + _rows.Length) % _rows.Length]);
        }

        return snapshot;
    }
}

// Status rows are written when something changed, and otherwise once per heartbeat so the file proves it is alive.
// Log diet (#6/#7): the same rule keeps coverage and health lines from repeating every tick.
internal static class HostJournalStatusPolicy
{
    internal const long HeartbeatTicks = TimeSpan.TicksPerMinute * 5;
    // The host-health tick is also 5 minutes of realtime; without slack its jitter would miss every other tick and
    // the effective heartbeat would be 10 minutes (advisor, D gate).
    internal const long HeartbeatToleranceTicks = TimeSpan.TicksPerSecond * 15;

    internal static bool ShouldWrite(string lastKey, string key, long lastWriteUtcTicks, long nowUtcTicks)
    {
        if (lastKey == null || !string.Equals(lastKey, key, StringComparison.Ordinal))
        {
            return true;
        }

        return nowUtcTicks - lastWriteUtcTicks >= HeartbeatTicks - HeartbeatToleranceTicks;
    }
}
