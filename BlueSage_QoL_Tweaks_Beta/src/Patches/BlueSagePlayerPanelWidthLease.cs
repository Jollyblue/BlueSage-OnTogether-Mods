using UnityEngine;

namespace BlueSage.QoLTweaks.Patches;

internal sealed class BlueSagePlayerPanelWidthLease : MonoBehaviour
{
    private RectTransform _background;
    private RectTransform _scrollView;
    private Vector2 _backgroundSize;
    private Vector2 _backgroundPosition;
    private Vector2 _scrollSize;
    private Vector2 _scrollPosition;
    private float _appliedExpansion;
    private bool _captured;

    internal float AppliedExpansion => _appliedExpansion;

    internal void Bind(
        RectTransform background,
        RectTransform scrollView)
    {
        if (_captured || background == null || scrollView == null)
        {
            return;
        }

        _background = background;
        _scrollView = scrollView;
        _backgroundSize = background.sizeDelta;
        _backgroundPosition = background.anchoredPosition;
        _scrollSize = scrollView.sizeDelta;
        _scrollPosition = scrollView.anchoredPosition;
        _captured = true;
    }

    internal void Apply(float expansion)
    {
        if (!_captured)
        {
            return;
        }

        float requested = Mathf.Max(0f, expansion);
        if (requested <= _appliedExpansion + 0.01f)
        {
            return;
        }

        _appliedExpansion = requested;
        ApplyRect(
            _background,
            _backgroundSize,
            _backgroundPosition,
            requested);
        ApplyRect(
            _scrollView,
            _scrollSize,
            _scrollPosition,
            requested);
        Canvas.ForceUpdateCanvases();
    }

    private static void ApplyRect(
        RectTransform rect,
        Vector2 baselineSize,
        Vector2 baselinePosition,
        float expansion)
    {
        if (rect == null)
        {
            return;
        }

        rect.sizeDelta = new Vector2(
            baselineSize.x + expansion,
            baselineSize.y);
        rect.anchoredPosition = new Vector2(
            baselinePosition.x + (expansion * 0.5f),
            baselinePosition.y);
    }

    private void OnDestroy()
    {
        ApplyRect(
            _background,
            _backgroundSize,
            _backgroundPosition,
            0f);
        ApplyRect(
            _scrollView,
            _scrollSize,
            _scrollPosition,
            0f);
    }
}
