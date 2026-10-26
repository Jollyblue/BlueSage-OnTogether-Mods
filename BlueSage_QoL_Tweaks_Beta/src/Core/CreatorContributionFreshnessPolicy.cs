namespace BlueSage.QoLTweaks.Core;

// Round 5 C2 step 1 (Blue, 2026-10-22: the BlueExclusive host role label refused to toggle on in F6 while two
// foreign rows sat outside Steam membership). The refusal used to say only "fresh exact creator ... authority
// is unavailable"; the predicate that actually failed was discarded. This policy names it, in the order the
// runtime checks it, so the next live log tells us which coupling to loosen in step 2. Pure: no Unity types.
internal static class CreatorContributionFreshnessPolicy
{
    // Returns an empty string when the host's own exact truth is fresh; otherwise the first failing reason.
    internal static string Describe(
        bool truthResolved,
        string truthError,
        bool lobbyKeyMatches,
        bool steamIdMatches,
        bool purrNetKnown,
        bool epochCurrent,
        bool generationCurrent,
        bool stillFresh)
    {
        if (!truthResolved)
        {
            string detail = string.IsNullOrWhiteSpace(truthError) ? "no detail" : truthError.Trim();
            return "the local exact identity truth did not resolve (" + detail + ")";
        }

        if (!lobbyKeyMatches)
        {
            return "the identity capture belongs to a different lobby than the one being hosted";
        }

        if (!steamIdMatches)
        {
            return "the resolved truth carries a different Steam id than the local player";
        }

        if (!purrNetKnown)
        {
            return "the local player's PurrNet id is not known to the identity capture yet";
        }

        if (!epochCurrent)
        {
            return "the identity truth is from an earlier lobby epoch";
        }

        if (!generationCurrent)
        {
            return "the identity truth is from an earlier evidence generation (the roster changed since it was captured)";
        }

        if (!stillFresh)
        {
            return "the identity truth lease expired before it was used";
        }

        return string.Empty;
    }
}
