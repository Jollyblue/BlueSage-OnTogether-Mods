using System;
using System.Globalization;
using UnityEngine;

namespace BlueSage.QoLTweaks.Core;

internal static class SelfMovementPolicy
{
    internal const float DefaultMultiplier = 1f;
    internal const float MinimumSpeedMultiplier = 0.25f;
    internal const float MaximumSpeedMultiplier = 5f;
    internal const float MinimumJumpMultiplier = 0.25f;
    internal const float MaximumJumpMultiplier = 5f;
    internal const float MinimumGravityMultiplier = 0.1f;
    internal const float MaximumGravityMultiplier = 5f;
    internal const float BaseFlySpeed = 8f;

    internal static float ClampSpeed(float value) => Mathf.Clamp(value, MinimumSpeedMultiplier, MaximumSpeedMultiplier);

    internal static float ClampJump(float value) => Mathf.Clamp(value, MinimumJumpMultiplier, MaximumJumpMultiplier);

    internal static float ClampGravity(float value) => Mathf.Clamp(value, MinimumGravityMultiplier, MaximumGravityMultiplier);

    internal static string FormatPosition(Vector3 position)
    {
        return string.Join(",",
            position.x.ToString("R", CultureInfo.InvariantCulture),
            position.y.ToString("R", CultureInfo.InvariantCulture),
            position.z.ToString("R", CultureInfo.InvariantCulture));
    }

    internal static bool TryParsePosition(string value, out Vector3 position)
    {
        position = Vector3.zero;
        string[] parts = (value ?? string.Empty)
            .Trim()
            .Replace(';', ',')
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 ||
            !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }

        position = new Vector3(x, y, z);
        return IsFinite(x) && IsFinite(y) && IsFinite(z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
