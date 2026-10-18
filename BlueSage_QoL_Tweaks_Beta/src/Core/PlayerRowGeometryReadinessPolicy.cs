using System;

namespace BlueSage.QoLTweaks.Core;

internal sealed class PlayerRowGeometryReadinessPolicy
{
    private bool _hasValidObservation;
    private bool _hasReachedReady;
    private float _width;
    private int _geometryFingerprint;

    internal bool Observe(float width, int geometryFingerprint)
    {
        if (float.IsNaN(width) ||
            float.IsInfinity(width) ||
            width <= 0f)
        {
            Reset();
            return false;
        }

        if (!_hasValidObservation ||
            !_width.Equals(width) ||
            _geometryFingerprint != geometryFingerprint)
        {
            bool keepStableRowRenderable = _hasReachedReady;
            _hasValidObservation = true;
            _width = width;
            _geometryFingerprint = geometryFingerprint;
            return keepStableRowRenderable;
        }

        _hasReachedReady = true;
        return true;
    }

    internal void Reset()
    {
        _hasValidObservation = false;
        _hasReachedReady = false;
        _width = 0f;
        _geometryFingerprint = 0;
    }
}
