using System;
using Cinemachine;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

// Private-field access for fly zoom, kept out of SelfMovementPatch so a game update that renames
// _currentZoom or _cinemachineVC disables only fly zoom instead of throwing in the movement patch's
// type initializer and stopping all movement.
internal static class FlyZoomAccess
{
    private static AccessTools.FieldRef<PlayerMovementController, float> _currentZoom;
    private static AccessTools.FieldRef<PlayerMovementController, CinemachineVirtualCamera> _virtualCamera;
    private static bool _initialized;
    private static bool _disabled;

    internal static bool TryGetZoom(PlayerMovementController instance, out float zoom)
    {
        zoom = 0f;
        if (instance == null || !EnsureInitialized())
        {
            return false;
        }

        zoom = _currentZoom(instance);
        return true;
    }

    internal static bool TrySetZoom(PlayerMovementController instance, float zoom)
    {
        if (instance == null || !EnsureInitialized())
        {
            return false;
        }

        _currentZoom(instance) = zoom;
        CinemachineVirtualCamera camera = _virtualCamera(instance);
        Cinemachine3rdPersonFollow thirdPersonRig = camera != null ? camera.GetCinemachineComponent<Cinemachine3rdPersonFollow>() : null;
        if (thirdPersonRig != null)
        {
            thirdPersonRig.CameraDistance = zoom;
        }
        return true;
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
            _currentZoom = AccessTools.FieldRefAccess<PlayerMovementController, float>("_currentZoom");
            _virtualCamera = AccessTools.FieldRefAccess<PlayerMovementController, CinemachineVirtualCamera>("_cinemachineVC");
        }
        catch (Exception ex)
        {
            _disabled = true;
            Plugin.Log?.LogWarning("Fly zoom disabled (game camera fields changed): " + ex.GetType().Name + ". Normal zoom and movement are unaffected.");
        }
        return !_disabled;
    }
}
