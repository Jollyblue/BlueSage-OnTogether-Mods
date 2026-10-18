using System;

namespace BlueSage.QoLTweaks.Core;

internal enum BlueSageHelpFrameKind
{
    QolMenu,
    StyleHelper
}

internal struct HelpFrameLayout
{
    public HelpFrameLayout(
        float contentHeight,
        float footerHeight,
        float stateLineHeight,
        float helpLineHeight,
        float fixedChromeHeight,
        float footerVerticalPadding,
        int actionRows,
        float actionRowHeight)
    {
        ContentHeight = contentHeight;
        FooterHeight = footerHeight;
        StateLineHeight = stateLineHeight;
        HelpLineHeight = helpLineHeight;
        FixedChromeHeight = fixedChromeHeight;
        FooterVerticalPadding = footerVerticalPadding;
        ActionRows = actionRows;
        ActionRowHeight = actionRowHeight;
    }

    public float ContentHeight { get; }

    public float FooterHeight { get; }

    public float StateLineHeight { get; }

    public float HelpLineHeight { get; }

    public float FixedChromeHeight { get; }

    public float FooterVerticalPadding { get; }

    public int ActionRows { get; }

    public float ActionRowHeight { get; }
}

internal static class HelpFramePolicy
{
    public static HelpFrameLayout Resolve(
        BlueSageHelpFrameKind kind,
        float windowWidth,
        float windowHeight)
    {
        bool styleHelper = kind == BlueSageHelpFrameKind.StyleHelper;
        bool compactStyle = styleHelper && windowWidth < 1100f;
        float fixedChromeHeight = styleHelper ? 78f : 152f;
        float desiredFooterHeight = styleHelper
            ? (compactStyle ? 198f : 156f)
            : 120f;
        int actionRows = styleHelper ? (compactStyle ? 2 : 1) : 0;
        float actionRowHeight = styleHelper ? 40f : 0f;
        float stateLineHeight = 22f;
        float helpLineHeight = 22f;
        float footerVerticalPadding = styleHelper ? 16f : 12f;
        float footerMinimum =
            stateLineHeight +
            helpLineHeight +
            footerVerticalPadding +
            (actionRows * actionRowHeight);
        float minimumContentHeight = styleHelper ? 36f : 24f;
        float maximumFooterHeight = Math.Max(
            footerMinimum,
            windowHeight - fixedChromeHeight - minimumContentHeight);
        float footerHeight = Math.Min(desiredFooterHeight, maximumFooterHeight);
        footerHeight = Math.Max(footerMinimum, footerHeight);
        float contentHeight = Math.Max(
            minimumContentHeight,
            windowHeight - fixedChromeHeight - footerHeight);

        if (contentHeight + footerHeight + fixedChromeHeight > windowHeight)
        {
            contentHeight = Math.Max(1f, windowHeight - fixedChromeHeight - footerHeight);
        }

        // The help line gets every pixel the footer has left, so long tooltips wrap onto extra lines
        // instead of being cut off at one 22px line (UAT F3FAB760BEB5, 2026-10-17).
        helpLineHeight = Math.Max(
            helpLineHeight,
            footerHeight - footerVerticalPadding - stateLineHeight - (actionRows * actionRowHeight));

        return new HelpFrameLayout(
            contentHeight,
            footerHeight,
            stateLineHeight,
            helpLineHeight,
            fixedChromeHeight,
            footerVerticalPadding,
            actionRows,
            actionRowHeight);
    }
}
