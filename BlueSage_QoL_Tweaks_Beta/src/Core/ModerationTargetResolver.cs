using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct ModerationTarget
{
    internal ModerationTarget(string steamId, string incidentId)
    {
        SteamId = steamId;
        IncidentId = incidentId;
    }

    internal string SteamId { get; }
    internal string IncidentId { get; }
}

internal static class ModerationTargetResolver
{
    internal static bool TryResolve(string query, IEnumerable<string> presentSteamIds, out ModerationTarget target, out string error)
    {
        target = default;
        error = "Use a clone incident ID, full SteamID64, or unique 6+ digit suffix.";
        string clean = (query ?? string.Empty).Trim();
        string[] present = (presentSteamIds ?? Array.Empty<string>())
            .Where(ModerationProtocolPolicy.IsSteamId64)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (CloneIncidentLedger.TryResolveIncident(clean, out CloneIncident incident))
        {
            if (!present.Contains(incident.SuspectSteamId, StringComparer.Ordinal))
            {
                error = incident.Id + " suspect is no longer present; no action was taken.";
                return false;
            }

            target = new ModerationTarget(incident.SuspectSteamId, incident.Id);
            return true;
        }

        if (ModerationProtocolPolicy.IsSteamId64(clean))
        {
            if (!present.Contains(clean, StringComparer.Ordinal))
            {
                error = "That SteamID64 is not uniquely present in the current lobby.";
                return false;
            }

            target = new ModerationTarget(clean, "none");
            return true;
        }

        if (clean.Length < 6 || clean.Any(c => !char.IsDigit(c)))
        {
            return false;
        }

        string[] matches = present.Where(id => id.EndsWith(clean, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            error = matches.Length > 1
                ? "That Steam ID suffix is ambiguous; use more digits or the full SteamID64."
                : "No current lobby member matches that Steam ID suffix.";
            return false;
        }

        target = new ModerationTarget(matches[0], "none");
        return true;
    }
}
