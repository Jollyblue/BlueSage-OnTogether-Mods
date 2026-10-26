using System;

namespace BlueSage.QoLTweaks.Core;

internal enum ReconnectGuardAction
{
    Noop,
    AllowReconnectMod,
    ScheduleManagedRetry,
    WaitForManagedRetryCadence,
    RunManagedRetryNow,
    StopManagedRetry,
}

internal sealed class ReconnectGuardDecision
{
    public ReconnectGuardDecision(ReconnectGuardAction action, string reason)
        : this(action, reason, 15, 20)
    {
    }

    public ReconnectGuardDecision(ReconnectGuardAction action, string reason, int recommendedAttemptIntervalSeconds, int recommendedMaxAttempts)
        : this(action, reason, recommendedAttemptIntervalSeconds, recommendedMaxAttempts, false)
    {
    }

    public ReconnectGuardDecision(ReconnectGuardAction action, string reason, int recommendedAttemptIntervalSeconds, int recommendedMaxAttempts, bool budgetSpent)
    {
        Action = action;
        Reason = reason;
        RecommendedAttemptIntervalSeconds = recommendedAttemptIntervalSeconds;
        RecommendedMaxAttempts = recommendedMaxAttempts;
        BudgetSpent = budgetSpent;
    }

    public ReconnectGuardAction Action { get; }
    public string Reason { get; }
    public int RecommendedAttemptIntervalSeconds { get; }
    public int RecommendedMaxAttempts { get; }

    // Round 5b: true only when the per-lobby generation budget stopped the guard; the plugin shows one notice.
    public bool BudgetSpent { get; }
}

internal sealed class ReconnectGuardInput
{
    public ReconnectGuardInput(
        bool guardEnabled,
        bool reconnectLoaded,
        bool suppressHostKickReconnect,
        bool blockDuringInitialLobbyGrace,
        bool requireVisibleLobby,
        int reconnectAttemptIntervalSeconds,
        int initialLobbyGraceSeconds,
        int maxGuardedAttempts,
        bool isHost,
        bool isIntentionalLeave,
        bool isAlreadyReconnecting,
        string notificationStatusName,
        float? secondsSinceLobbyCreated,
        float? secondsSinceLastManagedAttempt,
        int reconnectAttempt,
        int? visibleLobbyMemberCount,
        int? visibleLobbyMaxPlayers,
        int lobbyGenerationsUsed = 0,
        int maxGenerationsPerLobby = DefaultMaxGenerationsPerLobby,
        float? secondsSinceLastGuardActivity = null,
        int budgetIdleResetSeconds = DefaultBudgetIdleResetSeconds)
    {
        GuardEnabled = guardEnabled;
        ReconnectLoaded = reconnectLoaded;
        SuppressHostKickReconnect = suppressHostKickReconnect;
        BlockDuringInitialLobbyGrace = blockDuringInitialLobbyGrace;
        RequireVisibleLobby = requireVisibleLobby;
        ReconnectAttemptIntervalSeconds = reconnectAttemptIntervalSeconds;
        InitialLobbyGraceSeconds = initialLobbyGraceSeconds;
        MaxGuardedAttempts = maxGuardedAttempts;
        IsHost = isHost;
        IsIntentionalLeave = isIntentionalLeave;
        IsAlreadyReconnecting = isAlreadyReconnecting;
        NotificationStatusName = notificationStatusName ?? string.Empty;
        SecondsSinceLobbyCreated = secondsSinceLobbyCreated;
        SecondsSinceLastManagedAttempt = secondsSinceLastManagedAttempt;
        ReconnectAttempt = reconnectAttempt;
        VisibleLobbyMemberCount = visibleLobbyMemberCount;
        VisibleLobbyMaxPlayers = visibleLobbyMaxPlayers;
        LobbyGenerationsUsed = lobbyGenerationsUsed;
        MaxGenerationsPerLobby = maxGenerationsPerLobby;
        SecondsSinceLastGuardActivity = secondsSinceLastGuardActivity;
        BudgetIdleResetSeconds = budgetIdleResetSeconds;
    }

    // Round 5b (2026-10-23): guarded rejoins are budgeted per saved lobby. Inside one generation the guard was
    // already bounded; across generations it was not, and a host whose process and PurrNet outlived its Steam
    // lobby made every guest rejoin, drop with HostLost and start again.
    public const int DefaultMaxGenerationsPerLobby = 5;

    // The budget clears only after the guard has been idle this long (no generation running or finishing, no
    // budget stop). A rejoin that "gets in" for a few seconds and drops again therefore keeps counting (the
    // reviewer's case 1), while a manual rejoin that then plays for two minutes earns the lobby a fresh budget
    // on its next drop (case 2). Never cleared on the success confirmation itself.
    public const int DefaultBudgetIdleResetSeconds = 120;

    public bool GuardEnabled { get; }
    public bool ReconnectLoaded { get; }
    public bool SuppressHostKickReconnect { get; }
    public bool BlockDuringInitialLobbyGrace { get; }
    public bool RequireVisibleLobby { get; }
    public int ReconnectAttemptIntervalSeconds { get; }
    public int InitialLobbyGraceSeconds { get; }
    public int MaxGuardedAttempts { get; }
    public bool IsHost { get; }
    public bool IsIntentionalLeave { get; }
    public bool IsAlreadyReconnecting { get; }
    public string NotificationStatusName { get; }
    public float? SecondsSinceLobbyCreated { get; }
    public float? SecondsSinceLastManagedAttempt { get; }
    public int ReconnectAttempt { get; }
    public int? VisibleLobbyMemberCount { get; }
    public int? VisibleLobbyMaxPlayers { get; }
    public int LobbyGenerationsUsed { get; }
    public int MaxGenerationsPerLobby { get; }
    public float? SecondsSinceLastGuardActivity { get; }
    public int BudgetIdleResetSeconds { get; }

    public static ReconnectGuardInput DefaultForTests(
        bool guardEnabled = true,
        bool reconnectLoaded = false,
        bool suppressHostKickReconnect = true,
        bool blockDuringInitialLobbyGrace = true,
        bool requireVisibleLobby = true,
        int reconnectAttemptIntervalSeconds = 5,
        int initialLobbyGraceSeconds = 20,
        int maxGuardedAttempts = 3,
        bool isHost = false,
        bool isIntentionalLeave = false,
        bool isAlreadyReconnecting = false,
        string notificationStatusName = "HostLost",
        float? secondsSinceLobbyCreated = 30,
        float? secondsSinceLastManagedAttempt = null,
        int reconnectAttempt = 1,
        int? visibleLobbyMemberCount = 1,
        int? visibleLobbyMaxPlayers = 128,
        int lobbyGenerationsUsed = 0,
        int maxGenerationsPerLobby = DefaultMaxGenerationsPerLobby,
        float? secondsSinceLastGuardActivity = null,
        int budgetIdleResetSeconds = DefaultBudgetIdleResetSeconds)
    {
        return new ReconnectGuardInput(
            guardEnabled,
            reconnectLoaded,
            suppressHostKickReconnect,
            blockDuringInitialLobbyGrace,
            requireVisibleLobby,
            reconnectAttemptIntervalSeconds,
            initialLobbyGraceSeconds,
            maxGuardedAttempts,
            isHost,
            isIntentionalLeave,
            isAlreadyReconnecting,
            notificationStatusName,
            secondsSinceLobbyCreated,
            secondsSinceLastManagedAttempt,
            reconnectAttempt,
            visibleLobbyMemberCount,
            visibleLobbyMaxPlayers,
            lobbyGenerationsUsed,
            maxGenerationsPerLobby,
            secondsSinceLastGuardActivity,
            budgetIdleResetSeconds);
    }
}

internal static class ReconnectGuardPolicy
{
    public static ReconnectGuardDecision Evaluate(ReconnectGuardInput input)
    {
        if (!input.GuardEnabled)
        {
            return Allow("Reconnect guard disabled.");
        }

        if (input.IsHost)
        {
            return Allow("Host flow is not guarded.");
        }

        if (input.IsIntentionalLeave)
        {
            return Allow("Reconnect already sees this as an intentional leave.");
        }

        int intervalSeconds = ClampSeconds(input.ReconnectAttemptIntervalSeconds, 5, 30);
        int maxAttempts = ClampSeconds(input.MaxGuardedAttempts, 1, 3);

        if (input.IsAlreadyReconnecting)
        {
            return Noop("BlueSage managed reconnect is already active; suppressing overlapping reconnect pressure.", intervalSeconds, maxAttempts);
        }

        if (input.ReconnectAttempt > maxAttempts)
        {
            return Stop($"Stopping managed reconnect after guarded attempt limit ({input.ReconnectAttempt}/{maxAttempts}).", intervalSeconds, maxAttempts);
        }

        if (input.SuppressHostKickReconnect && LooksLikeHostKick(input.NotificationStatusName))
        {
            return Noop($"Host kick style disconnect ({input.NotificationStatusName}) detected; not auto reconnecting because the host may have removed this client intentionally.", intervalSeconds, maxAttempts);
        }

        // Round 5b: the per-lobby budget. Checked after the kick rule (a kicked player never hears "stopped after
        // 5 tries" for a rejoin that was never attempted) and before any schedule/run decision.
        int maxGenerations = ClampSeconds(input.MaxGenerationsPerLobby, 1, 10);
        int generationsUsed = BudgetClearsAfterIdle(input.SecondsSinceLastGuardActivity, input.BudgetIdleResetSeconds) ? 0 : input.LobbyGenerationsUsed;
        if (generationsUsed >= maxGenerations)
        {
            return new ReconnectGuardDecision(
                ReconnectGuardAction.StopManagedRetry,
                $"Reconnect budget for this lobby is spent ({generationsUsed}/{maxGenerations} guarded rejoins); not rejoining automatically. Rejoin from the menu when the host is back.",
                intervalSeconds,
                maxAttempts,
                budgetSpent: true);
        }

        if (input.BlockDuringInitialLobbyGrace &&
            input.SecondsSinceLobbyCreated.HasValue &&
            input.SecondsSinceLobbyCreated.Value >= 0f &&
            input.SecondsSinceLobbyCreated.Value < ClampSeconds(input.InitialLobbyGraceSeconds, 0, 300))
        {
            return Schedule($"Scheduling managed reconnect during lobby grace window ({input.SecondsSinceLobbyCreated.Value:0.0}s since lobby seen).", intervalSeconds, maxAttempts);
        }

        if (input.RequireVisibleLobby && (!input.VisibleLobbyMemberCount.HasValue || !input.VisibleLobbyMaxPlayers.HasValue))
        {
            return Schedule("Native disconnect recovery accepted; the visible lobby count is already unavailable.", intervalSeconds, maxAttempts);
        }

        int visibleMembers = input.VisibleLobbyMemberCount ?? 0;
        int visibleMaxPlayers = input.VisibleLobbyMaxPlayers ?? 0;

        if (input.RequireVisibleLobby && visibleMembers < 1)
        {
            return Schedule($"Native disconnect recovery accepted; the visible lobby count is now {visibleMembers}/{visibleMaxPlayers}.", intervalSeconds, maxAttempts);
        }

        if (input.SecondsSinceLastManagedAttempt.HasValue &&
            input.SecondsSinceLastManagedAttempt.Value >= 0f &&
            input.SecondsSinceLastManagedAttempt.Value < intervalSeconds)
        {
            return Wait($"Waiting for managed reconnect {intervalSeconds}s cadence ({input.SecondsSinceLastManagedAttempt.Value:0.0}s elapsed).", intervalSeconds, maxAttempts);
        }

        string takeover = input.ReconnectLoaded ? " AndrewLin Reconnect is loaded; BlueSage will suppress its transport-only retry." : string.Empty;
        return Run($"Visible lobby confirmed at {visibleMembers}/{visibleMaxPlayers} players; managed reconnect retry may run now.{takeover}", intervalSeconds, maxAttempts);
    }

    private static ReconnectGuardDecision Allow(string reason)
    {
        return new ReconnectGuardDecision(ReconnectGuardAction.AllowReconnectMod, reason);
    }

    private static ReconnectGuardDecision Noop(string reason, int intervalSeconds, int maxAttempts)
    {
        return new ReconnectGuardDecision(ReconnectGuardAction.Noop, reason, intervalSeconds, maxAttempts);
    }

    private static ReconnectGuardDecision Schedule(string reason, int intervalSeconds, int maxAttempts)
    {
        return new ReconnectGuardDecision(ReconnectGuardAction.ScheduleManagedRetry, reason, intervalSeconds, maxAttempts);
    }

    private static ReconnectGuardDecision Wait(string reason, int intervalSeconds, int maxAttempts)
    {
        return new ReconnectGuardDecision(ReconnectGuardAction.WaitForManagedRetryCadence, reason, intervalSeconds, maxAttempts);
    }

    private static ReconnectGuardDecision Run(string reason, int intervalSeconds, int maxAttempts)
    {
        return new ReconnectGuardDecision(ReconnectGuardAction.RunManagedRetryNow, reason, intervalSeconds, maxAttempts);
    }

    private static ReconnectGuardDecision Stop(string reason, int intervalSeconds, int maxAttempts)
    {
        return new ReconnectGuardDecision(ReconnectGuardAction.StopManagedRetry, reason, intervalSeconds, maxAttempts);
    }

    // Round 5b: the per-lobby budget clears only after the guard has been idle this long (see
    // ReconnectGuardInput.DefaultBudgetIdleResetSeconds). The plugin applies the same rule to its stored count.
    public static bool BudgetClearsAfterIdle(float? secondsSinceLastGuardActivity, int budgetIdleResetSeconds)
    {
        int resetSeconds = ClampSeconds(budgetIdleResetSeconds, 30, 900);
        return secondsSinceLastGuardActivity.HasValue &&
               secondsSinceLastGuardActivity.Value >= 0f &&
               secondsSinceLastGuardActivity.Value >= resetSeconds;
    }

    private static bool LooksLikeHostKick(string notificationStatusName)
    {
        string normalized = (notificationStatusName ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
        return normalized.IndexOf("kick", StringComparison.OrdinalIgnoreCase) >= 0 ||
               normalized.IndexOf("removedbyhost", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ClampSeconds(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
