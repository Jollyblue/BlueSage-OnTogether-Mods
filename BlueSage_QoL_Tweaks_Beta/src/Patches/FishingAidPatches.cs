using System;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

// Accessibility fishing aid (Blue, 2026-10-17), default OFF. Equip the rod with the game's fishing key
// (H) while standing near water: it finds water within casting range, casts, answers the bite, plays the
// reel mini-game, and recasts. A cast by hand sets a preferred spot. It drives vanilla's own
// fishing loop with single synthetic clicks, one per action (start cast, release cast, answer bite,
// reel click), each lasting one frame - the same as one click by the player, at most once per pause
// while waiting to cast. It never presses Release, so catches are kept like doing nothing.
internal static class FishingAidRuntime
{
    internal static int FakeClickFrame = -1;
    internal static int FakeReleaseFrame = -1;

    private static bool _hasRecordedCast;
    private static Vector3 _recordedSway;
    private static Vector3 _recordedSpot;
    private static bool _aidCasting;
    private static float _idleSince = -1f;
    private static float _biteSince = -1f;
    private static bool _biteAnswered;
    private static float _fishShownSince = -1f;
    private static bool _fishReleased;
    private static float _lastClickAt = -10f;
    private static bool _announcedTakeover;
    private static bool _announcedNoBait;
    private static bool _lastAttemptFoundNoWater;
    private static bool _announcedNoWater;
    private static bool _castAttemptPending;
    private static float _castAttemptAt;
    private static int _refusedCasts;
    private static bool _announcedRefused;
    private static float _refusedBackoffUntil;
    private static bool _rodWasOut;
    // A started cast that is still in state None after this long was refused by the game.
    private const float CastRefusedAfterSeconds = 0.5f;
    private const int RefusalsBeforeNotice = 3;

    internal static bool Enabled => Plugin.EnableFishingAid?.Value == true;

    // The Fishing Aid toggle changed: forget everything, including the hand-cast spot.
    internal static void Reset()
    {
        _hasRecordedCast = false;
        _fishShownSince = -1f;
        _fishReleased = false;
        _rodWasOut = false;
        ResetSession();
    }

    // One fishing session lasts from taking the rod out to putting it away. The hand-cast spot is kept
    // across sessions; ShouldForgetCast still drops it once the player walks away from it.
    private static void ResetSession()
    {
        _aidCasting = false;
        _idleSince = -1f;
        _biteSince = -1f;
        _biteAnswered = false;
        _announcedTakeover = false;
        _announcedNoBait = false;
        _lastAttemptFoundNoWater = false;
        _announcedNoWater = false;
        _castAttemptPending = false;
        _refusedCasts = 0;
        _announcedRefused = false;
    }

    internal static string Status()
    {
        if (!Enabled)
        {
            return "Fishing Aid: off. Turn it on in Toggles (Accessibility) or with /fishaid on.";
        }

        return _hasRecordedCast
            ? "Fishing Aid: on, casting at the spot you last cast by hand. Press your fishing key (H) to put the rod away and stop."
            : "Fishing Aid: on. Take out your rod with your fishing key (H) near water and stand still; it finds the water and fishes for you.";
    }

    internal static void RecordHandCast(FishingManager manager)
    {
        if (_aidCasting)
        {
            _aidCasting = false;
            return;
        }

        if (!FishingAidAccess.TryGetSway(manager, out Vector3 sway) || !TryGetPlayerPosition(out Vector3 spot))
        {
            return;
        }

        _recordedSway = sway;
        _recordedSpot = spot;
        _hasRecordedCast = true;
        // A hand cast after "no water found" lets the aid take over from that spot.
        _lastAttemptFoundNoWater = false;
        _announcedNoWater = false;
    }

    internal static void DriveFishingLoop(FishingManager manager)
    {
        if (!Enabled || manager == null || !TryGetPlayerPosition(out Vector3 position))
        {
            return;
        }

        float now = Time.unscaledTime;
        float metersAway = Vector3.Distance(new Vector3(position.x, 0f, position.z), new Vector3(_recordedSpot.x, 0f, _recordedSpot.z));
        if (FishingAidPolicy.ShouldForgetCast(_hasRecordedCast, metersAway))
        {
            // Walked away from the hand-cast spot: fall back to finding water automatically.
            _hasRecordedCast = false;
        }

        // Round 3: after a catch the game holds the fish on screen until Release/Recycle or its own
        // timeout. Press the game's own Release once, after the player has had a moment to see it.
        if (manager.IsFishShow)
        {
            if (_fishShownSince < 0f)
            {
                _fishShownSince = now;
                _fishReleased = false;
            }

            if (FishingAidPolicy.ShouldAutoRelease(true, true, _fishReleased, now - _fishShownSince, Plugin.FishingAidReleaseSeconds?.Value ?? FishingAidPolicy.DefaultReleaseSeconds))
            {
                _fishReleased = true;
                manager.ButtonReleaseFish();
            }
        }
        else
        {
            _fishShownSince = -1f;
            _fishReleased = false;
        }

        bool rodOut = manager.IsActive && manager.FishingCont != null && manager.FishingCont.FishingRod != null && manager.FishingCont.FishingRod.activeSelf;
        if (FishingAidPolicy.IsRodPutAway(_rodWasOut, rodOut))
        {
            // Round 3b: the rod went away, so taking it out again is a fresh session with a fresh
            // water search and its toasts shown again (they used to show only once per toggle).
            // The game's FishingController also hides the rod while each catch is shown, so every
            // recast starts a fresh session too: Blue wants that toast on each equip and recast
            // (UAT 2026-10-18), as a toast only, never a chat line.
            ResetSession();
        }

        _rodWasOut = rodOut;

        bool hasBait = manager.CurrentBait != BaitType.None;
        if (hasBait)
        {
            _announcedNoBait = false;
        }

        switch (manager.FishingState)
        {
            case FishingState.None:
                _biteSince = -1f;
                _biteAnswered = false;
                if (_idleSince < 0f)
                {
                    _idleSince = now;
                    _aidCasting = false;
                }

                if (!hasBait && rodOut && !_announcedNoBait)
                {
                    // Only when no bait is owned at all; with Reusable Consumables on, owned bait never runs out.
                    _announcedNoBait = true;
                    Plugin.Instance?.ShowToast("Fishing Aid is waiting: no bait equipped. Get some bait first (with Reusable Consumables on it never runs out).");
                }

                if (_castAttemptPending && now - _castAttemptAt >= CastRefusedAfterSeconds)
                {
                    // The game refused the cast (menu, mid-air, desk view...): back off, tell once.
                    _castAttemptPending = false;
                    _aidCasting = false;
                    _refusedBackoffUntil = now + 3f;
                    _refusedCasts++;
                    if (_refusedCasts >= RefusalsBeforeNotice && !_announcedRefused)
                    {
                        _announcedRefused = true;
                        Plugin.Instance?.ShowToast("Fishing Aid can't cast right now (a menu is open, you're in the air, or in desk view). It keeps trying.");
                    }
                }

                if (!_castAttemptPending &&
                    now >= _refusedBackoffUntil &&
                    IsCastAllowedNow() &&
                    FishingAidPolicy.ShouldStartCast(true, rodOut, true, manager.IsFishShow, IsPlayerIdle(), hasBait, now - _idleSince, _lastAttemptFoundNoWater))
                {
                    _aidCasting = true;
                    _castAttemptPending = true;
                    _castAttemptAt = now;
                    // The next attempt, if this one is refused, waits a full pause again.
                    _idleSince = now;
                    FishingAidInput.Press();
                }
                break;

            case FishingState.Casting:
                _idleSince = -1f;
                _castAttemptPending = false;
                _refusedCasts = 0;
                _announcedRefused = false;
                if (!_aidCasting)
                {
                    break;
                }

                // The remembered hand-cast spot first; if it no longer lands on water, search.
                bool aimed = (_hasRecordedCast && FishingAidAccess.TryAimRecordedCast(manager, _recordedSway)) ||
                             FishingAidAccess.TryAimAtWater(manager, PlayerForward());
                _lastAttemptFoundNoWater = !aimed;
                if (aimed)
                {
                    _announcedNoWater = false;
                    if (!_announcedTakeover)
                    {
                        _announcedTakeover = true;
                        Plugin.Instance?.ShowToast("Fishing Aid is fishing for you. Press your fishing key (H) to put the rod away and stop.");
                    }
                }
                else if (!_announcedNoWater)
                {
                    // 20 quick checks found no water: stop searching and hand control back.
                    _announcedNoWater = true;
                    Plugin.Instance?.ShowToast("Fishing Aid: no water found nearby. Try again (put the rod away and take it out), or cast once by hand and it takes over from there.");
                }

                // Releasing with no water lets vanilla cancel the cast cleanly; the aid retries shortly.
                FishingAidInput.Release();
                break;

            case FishingState.Fishing:
                _idleSince = -1f;
                bool alert = manager.FishingCont != null && manager.FishingCont.GetAlert();
                if (!alert)
                {
                    _biteSince = -1f;
                    _biteAnswered = false;
                    break;
                }

                if (_biteSince < 0f)
                {
                    _biteSince = now;
                }

                if (!_biteAnswered &&
                    FishingAidPolicy.ShouldAnswerBite(true, true, true, now - _biteSince, Plugin.FishingAidReactionSeconds?.Value ?? FishingAidPolicy.DefaultReactionSeconds))
                {
                    _biteAnswered = true;
                    FishingAidInput.Press();
                }
                break;

            default:
                _idleSince = -1f;
                break;
        }
    }

    internal static void PlayMiniGame(FishingManager manager)
    {
        if (!Enabled || !FishingAidAccess.TryReadMiniGame(manager, out int counter, out float fish, out float zone, out float degree))
        {
            return;
        }

        float now = Time.unscaledTime;
        float sinceClick = now - _lastClickAt;
        bool click =
            FishingAidPolicy.ShouldClickReelRound(true, counter, FishingAidPolicy.IsFishInZone(fish, zone, degree), sinceClick) ||
            FishingAidPolicy.ShouldClickLastRound(true, counter, sinceClick, Plugin.FishingAidClicksPerSecond?.Value ?? FishingAidPolicy.DefaultClicksPerSecond);
        if (click)
        {
            _lastClickAt = now;
            FishingAidInput.Press();
        }
    }

    // Mirrors the game's own cast gates that are cheap to read, so the aid does not try (and get
    // refused) while a game menu is open, the mouse is over game UI, or in desk view.
    private static bool IsCastAllowedNow()
    {
        try
        {
            InputManager input = MonoSingleton<InputManager>.I;
            return input != null &&
                   input.MouseOverUIObject == null &&
                   MonoSingleton<TaskManager>.I.IsReadyForContinueFishing() &&
                   MonoSingleton<OverlayManager>.I.ResolutionMode != ResolutionMode.Desk;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsPlayerIdle()
    {
        try
        {
            PlayerMovementController movement = NetworkSingleton<TextChannelManager>.I?.MainMovementController;
            return movement != null && movement.MovementState == MovementState.Idle;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static Vector3 PlayerForward()
    {
        try
        {
            Transform player = NetworkSingleton<TextChannelManager>.I?.MainPlayer;
            return player != null ? player.forward : Vector3.forward;
        }
        catch (Exception)
        {
            return Vector3.forward;
        }
    }

    private static bool TryGetPlayerPosition(out Vector3 position)
    {
        position = Vector3.zero;
        try
        {
            Transform player = NetworkSingleton<TextChannelManager>.I?.MainPlayer;
            if (player == null)
            {
                return false;
            }

            position = player.position;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

// Private-field access for the aid, isolated so a game update that renames a field disables only the aid.
internal static class FishingAidAccess
{
    private static AccessTools.FieldRef<FishingManager, Vector3> _castSway;
    private static AccessTools.FieldRef<FishingManager, bool> _isWater;
    private static AccessTools.FieldRef<FishingManager, int> _counter;
    private static AccessTools.FieldRef<FishingManager, RectTransform> _fishIcon;
    private static AccessTools.FieldRef<FishingManager, RectTransform> _clickZone;
    private static AccessTools.FieldRef<FishingManager, float> _clickZoneDegree;
    private static MethodInfo _castingRay;
    private static bool _initialized;
    private static bool _disabled;

    internal static bool TryGetSway(FishingManager manager, out Vector3 sway)
    {
        sway = Vector3.zero;
        if (manager == null || !EnsureInitialized())
        {
            return false;
        }

        sway = _castSway(manager);
        return true;
    }

    // Points the cast where the player last cast by hand, then lets vanilla's CastingRay clamp it to the
    // rod's range and confirm it lands on water.
    internal static bool TryAimRecordedCast(FishingManager manager, Vector3 sway)
    {
        if (manager == null || !EnsureInitialized())
        {
            return false;
        }

        try
        {
            _castSway(manager) = sway;
            bool water = (bool)_castingRay.Invoke(manager, null);
            _isWater(manager) = water;
            return water;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return false;
        }
    }

    // No hand cast needed: try aim points from FishingAidPolicy.CandidateCastOffsets (straight ahead
    // first at medium range, then alternating left/right; at most 20) within the rod's range; vanilla CastingRay says which one is water.
    internal static bool TryAimAtWater(FishingManager manager, Vector3 forward)
    {
        if (manager == null || !EnsureInitialized())
        {
            return false;
        }

        try
        {
            float maxRange = manager.FishingRodSettings.CastRange;
            float minRange = ScriptableSingleton<FishingSettings>.I.HookReelDistanceCancel;
            foreach ((float x, float z) in FishingAidPolicy.CandidateCastOffsets(forward.x, forward.z, maxRange, minRange))
            {
                _castSway(manager) = new Vector3(x, 0f, z);
                if ((bool)_castingRay.Invoke(manager, null))
                {
                    _isWater(manager) = true;
                    return true;
                }
            }

            _isWater(manager) = false;
            return false;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return false;
        }
    }

    internal static bool TryReadMiniGame(FishingManager manager, out int counter, out float fishDegrees, out float zoneDegrees, out float zoneDegree)
    {
        counter = 0;
        fishDegrees = zoneDegrees = zoneDegree = 0f;
        if (manager == null || !EnsureInitialized())
        {
            return false;
        }

        try
        {
            counter = _counter(manager);
            fishDegrees = _fishIcon(manager).rotation.eulerAngles.z;
            zoneDegrees = _clickZone(manager).rotation.eulerAngles.z;
            zoneDegree = _clickZoneDegree(manager);
            return true;
        }
        catch (Exception ex)
        {
            Disable(ex);
            return false;
        }
    }

    private static bool EnsureInitialized()
    {
        if (_initialized)
        {
            return !_disabled;
        }

        _initialized = true;
        try
        {
            _castSway = AccessTools.FieldRefAccess<FishingManager, Vector3>("_castSway");
            _isWater = AccessTools.FieldRefAccess<FishingManager, bool>("_isWater");
            _counter = AccessTools.FieldRefAccess<FishingManager, int>("_counter");
            _fishIcon = AccessTools.FieldRefAccess<FishingManager, RectTransform>("_fishIcon");
            _clickZone = AccessTools.FieldRefAccess<FishingManager, RectTransform>("_clickZone");
            _clickZoneDegree = AccessTools.FieldRefAccess<FishingManager, float>("_clickZoneDegree");
            _castingRay = AccessTools.Method(typeof(FishingManager), "CastingRay") ?? throw new MissingMethodException("FishingManager.CastingRay");
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
        return !_disabled;
    }

    private static void Disable(Exception ex)
    {
        _disabled = true;
        Plugin.Log?.LogWarning("Fishing Aid disabled (game fishing code changed): " + ex.GetType().Name + ". Normal fishing is unaffected.");
    }
}

// One synthetic click or release for this frame. The value is written straight into InputManager
// (its auto-property setter) for readers this frame, and the getter postfixes below cover readers
// that run after InputManager refreshes from real input (e.g. the bite coroutine), in case either
// path is skipped by inlining.
internal static class FishingAidInput
{
    private static readonly MethodInfo DownSetter = AccessTools.PropertySetter(typeof(InputManager), nameof(InputManager.IsMouseButton0Down));
    private static readonly MethodInfo UpSetter = AccessTools.PropertySetter(typeof(InputManager), nameof(InputManager.IsMouseButton0Up));

    internal static void Press()
    {
        FishingAidRuntime.FakeClickFrame = Time.frameCount;
        Set(DownSetter);
    }

    internal static void Release()
    {
        FishingAidRuntime.FakeReleaseFrame = Time.frameCount;
        Set(UpSetter);
    }

    private static void Set(MethodInfo setter)
    {
        try
        {
            InputManager input = MonoSingleton<InputManager>.I;
            if (input != null && setter != null)
            {
                setter.Invoke(input, new object[] { true });
            }
        }
        catch (Exception)
        {
            // The getter postfix still supplies the click.
        }
    }
}

[HarmonyPatch(typeof(FishingManager), "CastFishingRod")]
internal static class FishingAidRecordCastPatch
{
    [HarmonyPrefix]
    private static void Prefix(FishingManager __instance)
    {
        try
        {
            FishingAidRuntime.RecordHandCast(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Fishing Aid cast record skipped: " + ex.GetType().Name + ".");
        }
    }
}

[HarmonyPatch(typeof(FishingManager), "Update")]
internal static class FishingAidLoopPatch
{
    [HarmonyPrefix]
    private static void Prefix(FishingManager __instance)
    {
        try
        {
            FishingAidRuntime.DriveFishingLoop(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Fishing Aid step skipped: " + ex.GetType().Name + ".");
        }
    }
}

[HarmonyPatch(typeof(FishingManager), "FishMiniGameUpdate")]
internal static class FishingAidMiniGamePatch
{
    // Runs right after vanilla rotates the fish this frame and right before Update reads the click,
    // so the zone test sees exactly the position the game will judge.
    [HarmonyPostfix]
    private static void Postfix(FishingManager __instance)
    {
        try
        {
            FishingAidRuntime.PlayMiniGame(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Fishing Aid mini-game step skipped: " + ex.GetType().Name + ".");
        }
    }
}

[HarmonyPatch(typeof(InputManager), nameof(InputManager.IsMouseButton0Down), MethodType.Getter)]
internal static class FishingAidClickInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result |= FishingAidRuntime.FakeClickFrame == Time.frameCount;
    }
}

[HarmonyPatch(typeof(InputManager), nameof(InputManager.IsMouseButton0Up), MethodType.Getter)]
internal static class FishingAidReleaseInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        __result |= FishingAidRuntime.FakeReleaseFrame == Time.frameCount;
    }
}
