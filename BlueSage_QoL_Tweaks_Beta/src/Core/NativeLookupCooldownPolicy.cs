namespace BlueSage.QoLTweaks.Core;

// Round 4 WS5: after a native manager lookup finds nothing (main menu, loading), wait before scanning every
// loaded object again. A clock that went backwards (scene or session reset) always allows a fresh scan.
internal static class NativeLookupCooldownPolicy
{
    internal const float MissCooldownSeconds = 5f;

    internal static bool ShouldScan(float lastMissAt, float now, float cooldownSeconds)
    {
        return lastMissAt < 0f || now < lastMissAt || now - lastMissAt >= cooldownSeconds;
    }
}
