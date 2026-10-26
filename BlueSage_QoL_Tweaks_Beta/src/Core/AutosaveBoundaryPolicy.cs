namespace BlueSage.QoLTweaks.Core;

// Round 4 Stage G (Marbles, 2026-10-20). The game autosaves every 180 s on the main thread (DataManager.Update,
// saveTimer >= 180f), then writes backup copies in a coroutine on the following frames. A BlueSage sweep that
// starts in that window stacks its stall onto the game's. No fixed interval avoids it (900 s is 5 x 180 s and
// stayed phase-locked in Marbles' log), so the sweep loop asks this rule with the game's live timer when it is due.
internal static class AutosaveBoundaryPolicy
{
    internal const float AutosavePeriodSeconds = 180f;
    internal const float BeforeWindowSeconds = 30f;
    internal const float AfterWindowSeconds = 20f;

    internal static bool ShouldDefer(
        float saveTimerSeconds,
        float periodSeconds = AutosavePeriodSeconds,
        float beforeSeconds = BeforeWindowSeconds,
        float afterSeconds = AfterWindowSeconds)
    {
        if (float.IsNaN(saveTimerSeconds) || float.IsInfinity(saveTimerSeconds) || saveTimerSeconds < 0f ||
            float.IsNaN(periodSeconds) || float.IsInfinity(periodSeconds) || periodSeconds <= 0f)
        {
            // Unknown timer: never block the sweep on a guess.
            return false;
        }

        if (saveTimerSeconds < afterSeconds)
        {
            return true;
        }

        return saveTimerSeconds >= periodSeconds - beforeSeconds;
    }
}
