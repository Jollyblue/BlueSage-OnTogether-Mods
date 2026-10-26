namespace BlueSage.QoLTweaks.Core;

internal sealed class IdentityResyncHelpPresentation
{
    internal IdentityResyncHelpPresentation(
        bool healthy,
        string statusHeading,
        string whatThisMeans,
        string whatToDo,
        string refreshButtonLabel,
        string refreshExplanation,
        string recheckButtonLabel,
        string recheckExplanation,
        string expectedResult,
        string recoveryAdvice)
    {
        Healthy = healthy;
        StatusHeading = statusHeading;
        WhatThisMeans = whatThisMeans;
        WhatToDo = whatToDo;
        RefreshButtonLabel = refreshButtonLabel;
        RefreshExplanation = refreshExplanation;
        RecheckButtonLabel = recheckButtonLabel;
        RecheckExplanation = recheckExplanation;
        ExpectedResult = expectedResult;
        RecoveryAdvice = recoveryAdvice;
    }

    internal bool Healthy { get; }
    internal string StatusHeading { get; }
    internal string WhatThisMeans { get; }
    internal string WhatToDo { get; }
    internal string RefreshButtonLabel { get; }
    internal string RefreshExplanation { get; }
    internal string RecheckButtonLabel { get; }
    internal string RecheckExplanation { get; }
    internal string ExpectedResult { get; }
    internal string RecoveryAdvice { get; }
}

internal static class IdentityResyncHelpPolicy
{
    // The controller's success replies start with these; FormatActionResult keys on them.
    internal const string RefreshDonePrefix = "Player list refreshed.";
    internal const string RecheckDonePrefix = "Player matching recheck finished.";

    internal static string FormatActionResult(
        string action,
        string rawResult,
        IdentityResyncCoverage coverage)
    {
        rawResult = rawResult ?? string.Empty;
        if (action == "refresh" &&
            rawResult.StartsWith(RefreshDonePrefix, System.StringComparison.Ordinal))
        {
            return coverage.Ready
                ? "Player list refreshed. Every visible player is still matched safely. No player action was taken."
                : "Player list refreshed. Some rows still need matching. Wait a few seconds, then use Recheck Player Matches only if the list still looks wrong. No player action was taken.";
        }

        if (action == "repair" &&
            rawResult.StartsWith(RecheckDonePrefix, System.StringComparison.Ordinal))
        {
            return coverage.Ready
                ? "Player matching recheck finished. Every visible player is matched safely. No player action was taken."
                : "Player matching recheck finished. Unproven rows stayed locked so the wrong player cannot be targeted. Ask the affected player to leave and rejoin if the list still looks wrong.";
        }

        if (action == "refresh")
        {
            return "The player list could not refresh yet. Close and reopen Host Tools, then try once more. No player action was taken.";
        }

        if (action == "request")
        {
            return "The host recheck request could not be sent. Ask the host to open Host Tools and choose Recheck Player Matches. No player action was taken.";
        }

        if (action == "repair")
        {
            return "Player matching was not changed. Only the current lobby host can run this recheck, or the lobby is still settling. Wait a few seconds and try once more. No player action was taken.";
        }

        return "Nothing changed. No player action was taken.";
    }

    internal static IdentityResyncHelpPresentation Describe(
        IdentityResyncCoverage coverage,
        bool isHost)
    {
        string refreshExplanation =
            "Use when a name, badge, or button looks stale. It redraws only BlueSage's player list from information already in the game. Safe to press: it does not kick, mute, report, ban, or delete anyone.";
        string recheckExplanation = isHost
            ? "Use only if Refresh Player List did not fix the row or player actions stay unavailable. BlueSage waits for joins and leaves to settle, compares each visible row with Steam and the game network once, then refreshes its safe matches."
            : "Use only if Refresh Player List did not fix the row. This asks the host to compare each visible row with Steam and the game network once; it does not give you host controls or act on a player.";
        string expectedResult =
            "Expected result: matched rows become usable. If a row still cannot be proven, the affected rows stay locked and no player is removed.";

        if (coverage.Ready)
        {
            return new IdentityResyncHelpPresentation(
                healthy: true,
                statusHeading: "Player matching is healthy",
                whatThisMeans: "Every visible player row agrees with the game, Steam lobby, and network. BlueSage can safely aim host actions at the right person.",
                whatToDo: "Nothing right now. BlueSage keeps player matching up to date automatically as players join or leave. Use the steps below only if a row later looks missing, duplicated, or attached to the wrong name.",
                refreshButtonLabel: "1. Refresh Player List",
                refreshExplanation: refreshExplanation,
                recheckButtonLabel: isHost
                    ? "2. Recheck Player Matches"
                    : "Ask Host to Recheck Player List",
                recheckExplanation: recheckExplanation,
                expectedResult: expectedResult,
                recoveryAdvice: "If a row ever looks wrong: start with Refresh Player List. Use Recheck Player Matches only if refreshing does not fix it.");
        }

        return new IdentityResyncHelpPresentation(
            healthy: false,
            statusHeading: "Some player rows are still being matched",
            whatThisMeans: "The game, Steam lobby, and network do not agree yet for every row. BlueSage keeps host actions locked on the rows it can't match, so the wrong person cannot be targeted; matched players can still be managed.",
            whatToDo: "BlueSage is already checking again. Wait a few seconds after anyone joins or leaves. If the list still looks wrong, use step 1. The host should use step 2 only if step 1 does not fix it.",
            refreshButtonLabel: "1. Refresh Player List",
            refreshExplanation: refreshExplanation,
            recheckButtonLabel: isHost
                ? "2. Recheck Player Matches"
                : "Ask Host to Recheck Player List",
            recheckExplanation: recheckExplanation,
            expectedResult: expectedResult,
            recoveryAdvice: "If it still will not match: ask the affected player to leave and rejoin. If several rows remain wrong, return to the main menu and recreate the lobby. BlueSage refuses to guess who a row belongs to.");
    }
}
