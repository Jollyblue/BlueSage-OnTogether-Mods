using System;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(PlayerMovementController))]
internal static class SelfMovementPatch
{
    private static Vector3? _pendingTeleport;

    internal static void QueueTeleport(Vector3 target)
    {
        OptInRescuePatch.Stop();
        ResetVerticalVelocity();
        _pendingTeleport = target;
    }

    internal static void CleanupRuntimeState()
    {
        _pendingTeleport = null;
        BetterMoveState.IsSlowWalkActive = false;
        ResetVerticalVelocity();
    }

    internal static void ResetVerticalVelocity()
    {
        try
        {
            PlayerMovementController movement = NetworkSingleton<TextChannelManager>.I?.MainMovementController;
            if (movement != null)
            {
                movement.SpeedY = 0f;
            }
        }
        catch
        {
            // Scene teardown can invalidate the network singleton between frames.
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("MovePlayer")]
    private static bool MovePlayerPrefix(PlayerMovementController __instance, ref CharacterController ____characterController)
    {
        if (!IsLocalMainMovement(__instance))
        {
            return true;
        }

        if (_pendingTeleport.HasValue)
        {
            Vector3 target = _pendingTeleport.Value;
            _pendingTeleport = null;
            ApplyPosition(__instance, ____characterController, target);
            TryForceSyncLocalOwner();
            Plugin.Log?.LogInfo("Self movement: saved-position teleport applied to " + SelfMovementPolicy.FormatPosition(target) + ".");
            return false;
        }

        return !Plugin.ShouldApplyNoclipFly;
    }

    private static void TryForceSyncLocalOwner()
    {
        try
        {
            NetworkSingleton<TextChannelManager>.I?.MainNetTransform?.ForceSync();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Self movement: owner transform sync will fall back to the normal network tick: " + ex.GetType().Name + ".");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("MovePlayer")]
    private static void MovePlayerPostfix(PlayerMovementController __instance, ref CharacterController ____characterController)
    {
        if (IsLocalMainMovement(__instance))
        {
            ApplyMovementTuning(__instance, ____characterController);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("Update")]
    private static void UpdatePostfix(PlayerMovementController __instance)
    {
        if (!Plugin.ShouldApplyNoclipFly || !IsLocalMainMovement(__instance))
        {
            return;
        }

        try
        {
            BetterMoveState.IsSlowWalkActive = false;
            Vector3 direction = Vector3.zero;
            InputManager input = MonoSingleton<InputManager>.I;
            ApplyFlyRotation(__instance, input);
            if (input != null)
            {
                direction += Flatten(__instance.transform.forward) * input.Vertical;
                direction += Flatten(__instance.transform.right) * input.Horizontal;
            }

            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E))
            {
                direction += Vector3.up;
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.Q))
            {
                direction += Vector3.down;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float controlMultiplier = 1f;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                controlMultiplier = 2.5f;
            }
            else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                controlMultiplier = FlyCameraPolicy.PrecisionMultiplier(
                    Plugin.FlyPrecisionPercent?.Value ?? FlyCameraPolicy.DefaultPrecisionPercent);
            }

            float flySpeed = SelfMovementPolicy.BaseFlySpeed * Plugin.LockedSelfSpeedMultiplier;
            Vector3 target = __instance.transform.position + direction.normalized * flySpeed * controlMultiplier * Time.deltaTime;
            ApplyPosition(__instance, null, target);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Self movement fly update skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool _flyRotateLockedCursor;
    private static string _cachedRotateButtonName;
    private static KeyCode _cachedRotateKey = KeyCode.Mouse2;

    // Vanilla rotates the camera only while right mouse is held (SetScreenCameraRotation). In fly
    // mode the chosen button drives the same vanilla SetCameraRotation, so speed/limits match.
    // Right mouse held means vanilla is already rotating this frame; skip to avoid double speed.
    private static void ApplyFlyRotation(PlayerMovementController instance, InputManager input)
    {
        string button = Plugin.FlyRotateButton?.Value ?? FlyCameraPolicy.DefaultRotateButton;
        if (!string.Equals(button, _cachedRotateButtonName, StringComparison.Ordinal))
        {
            _cachedRotateButtonName = button;
            _cachedRotateKey = (KeyCode)Enum.Parse(typeof(KeyCode), FlyCameraPolicy.NormalizeRotateButton(button));
        }

        bool held = Input.GetKey(_cachedRotateKey);
        if (input != null && !input.IsMouseButton1 && FlyCameraPolicy.ShouldDriveRotation(true, held, button))
        {
            instance.SetCameraRotation(new Vector2(input.MouseX, input.MouseY));
            Cursor.lockState = CursorLockMode.Locked;
            _flyRotateLockedCursor = true;
            return;
        }

        ReleaseFlyRotateCursor(input);
    }

    internal static void ReleaseFlyRotateCursor()
    {
        ReleaseFlyRotateCursor(MonoSingleton<InputManager>.I);
    }

    private static void ReleaseFlyRotateCursor(InputManager input)
    {
        if (_flyRotateLockedCursor && (input == null || !input.IsMouseButton1))
        {
            Cursor.lockState = CursorLockMode.None;
            _flyRotateLockedCursor = false;
        }
    }

    // Scrolling a BlueSage window (menu, guide, welcome card) never zooms the camera. With Extended
    // Camera Zoom on, vanilla Zoom()'s GameSettings.ZoomMin/ZoomMax clamp gets a wider range
    // everywhere; desk mode and vanilla's UI-hover rule stay exactly as vanilla has them.
    [HarmonyPrefix]
    [HarmonyPatch("Zoom")]
    private static bool ZoomPrefix(PlayerMovementController __instance)
    {
        if (!IsLocalMainMovement(__instance))
        {
            return true;
        }

        if (BlueSageWindowHoverScope.IsScreenMouseOverAnyWindow())
        {
            return false;
        }

        if (Plugin.EnableExtendedZoom?.Value != true)
        {
            return true;
        }

        try
        {
            InputManager input = MonoSingleton<InputManager>.I;
            if (input == null || MonoSingleton<OverlayManager>.I.ResolutionMode == ResolutionMode.Desk)
            {
                return true;
            }

            if (input.MouseOverUIObject != null &&
                !MonoSingleton<UIManager>.I.OverlayMasks.Contains(input.MouseOverUIObject.transform))
            {
                return false;
            }

            if (!FlyZoomAccess.TryGetZoom(__instance, out float current))
            {
                return true;
            }

            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            float zoom = FlyCameraPolicy.ComputeFlyZoom(
                current,
                Input.mouseScrollDelta.y,
                settings.ZoomSpeed,
                settings.ZoomMin,
                settings.ZoomMax,
                input.IsShiftKey);
            return !FlyZoomAccess.TrySetZoom(__instance, zoom);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogDebug("Fly zoom fell back to vanilla: " + ex.GetType().Name + ".");
            return true;
        }
    }

    internal static void RestoreVanillaCamera()
    {
        try
        {
            ReleaseFlyRotateCursor(MonoSingleton<InputManager>.I);
            PlayerMovementController movement = NetworkSingleton<TextChannelManager>.I?.MainMovementController;
            if (movement == null || !FlyZoomAccess.TryGetZoom(movement, out float current))
            {
                return;
            }

            GameSettings settings = ScriptableSingleton<GameSettings>.I;
            FlyZoomAccess.TrySetZoom(movement, FlyCameraPolicy.ClampToVanilla(current, settings.ZoomMin, settings.ZoomMax));
        }
        catch
        {
            // Scene teardown can invalidate the camera between frames; vanilla clamps on next scroll.
        }
    }
    private static void ApplyMovementTuning(PlayerMovementController instance, CharacterController controller)
    {
        if (!Plugin.ShouldApplySelfMovementTuning || Plugin.ShouldApplyNoclipFly)
        {
            return;
        }

        try
        {
            InputManager input = MonoSingleton<InputManager>.I;
            if (input == null)
            {
                return;
            }

            float speedMultiplier = Plugin.LockedSelfSpeedMultiplier;
            if (Math.Abs(speedMultiplier - 1f) > 0.001f)
            {
                Vector3 direction = Flatten(instance.transform.forward) * input.Vertical +
                                    Flatten(instance.transform.right) * input.Horizontal;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    float baseSpeed = ScriptableSingleton<GameSettings>.I.WalkSpeed;
                    ApplyDelta(instance, controller, direction.normalized * (baseSpeed * (speedMultiplier - 1f) * Time.deltaTime));
                }
            }

            float jumpMultiplier = Plugin.LockedSelfJumpMultiplier;
            if (Math.Abs(jumpMultiplier - 1f) > 0.001f && Input.GetKeyDown(KeyCode.Space) && instance.SpeedY > 0f)
            {
                instance.SpeedY = Mathf.Max(instance.SpeedY, ScriptableSingleton<GameSettings>.I.JumpSpeed * jumpMultiplier);
            }

            float gravityMultiplier = Plugin.LockedSelfGravityMultiplier;
            if (Math.Abs(gravityMultiplier - 1f) > 0.001f && instance.SpeedY < 0f)
            {
                instance.SpeedY -= ScriptableSingleton<GameSettings>.I.FallAcceleration * (gravityMultiplier - 1f) * Time.deltaTime;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Self movement tuning skipped: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void ApplyDelta(PlayerMovementController instance, CharacterController controller, Vector3 delta)
    {
        if (controller != null)
        {
            controller.Move(delta);
        }
        else if (instance != null)
        {
            instance.transform.position += delta;
        }

        TextChannelManager chat = NetworkSingleton<TextChannelManager>.I;
        if (chat?.MainPlayer != null && (controller == null || chat.MainPlayer != controller.transform))
        {
            chat.MainPlayer.position += delta;
        }
    }

    private static bool IsLocalMainMovement(PlayerMovementController instance)
    {
        try
        {
            return instance != null && instance == NetworkSingleton<TextChannelManager>.I?.MainMovementController;
        }
        catch
        {
            return false;
        }
    }

    private static void ApplyPosition(PlayerMovementController instance, CharacterController controller, Vector3 target)
    {
        if (controller != null)
        {
            controller.transform.position = target;
        }

        if (instance != null)
        {
            instance.transform.position = target;
        }

        TextChannelManager chat = NetworkSingleton<TextChannelManager>.I;
        if (chat?.MainPlayer != null)
        {
            chat.MainPlayer.position = target;
        }
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude <= 0.0001f ? Vector3.zero : value.normalized;
    }
}
