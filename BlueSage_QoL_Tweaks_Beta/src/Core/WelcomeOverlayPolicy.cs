namespace BlueSage.QoLTweaks.Core;

internal static class WelcomeOverlayPolicy
{
    // Round 3b (Blue, 2026-10-18): once per lobby visit, so every join, reconnect and lobby-hop offers
    // it. "Don't show this again" turns the toggle off (enabled = false).
    internal static bool ShouldOffer(
        bool enabled,
        string currentVisitKey,
        string lastOfferedVisitKey)
    {
        if (!enabled || string.IsNullOrEmpty(currentVisitKey))
        {
            return false;
        }

        return !WelcomeLobbyKeyPolicy.IsSameVisit(currentVisitKey, lastOfferedVisitKey);
    }

    // Gaps match DrawWelcomeOverlayWindowContents: 6 px after the title, 10 px after the body, 6 px
    // between buttons. Margins are summed rather than collapsed, so the estimate errs a few px tall,
    // never short (Blue, UAT 2026-10-18: the fourth button was cut off).
    internal const float TitleGap = 6f;
    internal const float BodyGap = 10f;
    internal const float ButtonGap = 6f;

    internal static float WindowHeight(
        float paddingVertical,
        float titleHeight,
        float bodyHeight,
        int buttonCount,
        float buttonHeight,
        float labelMarginVertical,
        float buttonMarginVertical)
    {
        int buttons = System.Math.Max(0, buttonCount);
        return paddingVertical +
               titleHeight + labelMarginVertical + TitleGap +
               bodyHeight + labelMarginVertical + BodyGap +
               (buttons * (buttonHeight + buttonMarginVertical)) +
               (System.Math.Max(0, buttons - 1) * ButtonGap);
    }

    internal static string BuildBody(
        string version,
        string qolMenuKeyName,
        string styleKeyName,
        string flyKeyName,
        string sweepKeyName)
    {
        string menuLine = HotkeyLabelPolicy.IsUnbound(qolMenuKeyName) ? "/qolmenu opens the QoL menu." : qolMenuKeyName + " opens the QoL menu.";
        string styleLine = HotkeyLabelPolicy.IsUnbound(styleKeyName) ? "/styleui opens Style Helper." : styleKeyName + " opens Style Helper.";
        string flyLine = HotkeyLabelPolicy.IsUnbound(flyKeyName) ? "Fly has no key yet; pick one with /rebind fly." : flyKeyName + " toggles fly, just for you.";
        string sweepLine = HotkeyLabelPolicy.IsUnbound(sweepKeyName) ? "/sweep now runs a cleanup right away." : sweepKeyName + " runs a cleanup right away.";

        return "BlueSage QoL v" + version + " -- quick start:\n\n" +
               menuLine + " " + styleLine + "\n" +
               flyLine + " " + sweepLine + "\n\n" +
               "All four are rebindable right here, in the menu's Hotkeys section, or with /rebind.\n\n" +
               "Type / in chat to browse every command; Tab completes it.\n" +
               "/qol status shows what's on. The README has the full FAQ and feature list.";
    }
}
