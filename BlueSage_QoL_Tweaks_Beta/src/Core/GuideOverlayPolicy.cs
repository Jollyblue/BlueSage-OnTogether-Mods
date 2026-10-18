#nullable enable

using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct GuideSection
{
    internal GuideSection(string heading, string body)
    {
        Heading = heading;
        Body = body;
    }

    internal string Heading { get; }
    internal string Body { get; }
}

// The /bsqol guide pop-up (UAT F3FAB760BEB5, 2026-10-17): the whole stack explained in cozy,
// casual-gamer words. Hotkeys come from the player's live bindings so the guide never lies.
internal static class GuideOverlayPolicy
{
    internal static IReadOnlyList<GuideSection> BuildSections(
        string version,
        string qolMenuKeyName,
        string styleKeyName,
        string flyKeyName,
        string sweepKeyName)
    {
        string menu = HotkeyLabelPolicy.IsUnbound(qolMenuKeyName) ? "/qolmenu" : qolMenuKeyName.Trim();
        string style = HotkeyLabelPolicy.IsUnbound(styleKeyName) ? "/styleui" : styleKeyName.Trim();
        string fly = HotkeyLabelPolicy.IsUnbound(flyKeyName)
            ? "Fly has no key yet; pick one with /rebind fly."
            : flyKeyName.Trim() + " toggles fly, just for you.";
        string sweep = HotkeyLabelPolicy.IsUnbound(sweepKeyName)
            ? "/sweep now tidies up right away."
            : sweepKeyName.Trim() + " tidies up right away.";

        return new[]
        {
            new GuideSection(
                "Welcome in!",
                "BlueSage QoL v" + version + " adds comfy extras to On-Together. Everything starts with friendly " +
                "defaults, so you can just play and explore one thing at a time."),
            new GuideSection(
                "Your keys",
                menu + " opens the QoL menu. " + style + " opens Style Helper for names and status. " + fly + " " + sweep +
                " While flying, hold Alt for tiny careful moves and the middle mouse button to turn the camera. " +
                "Scroll zooms extra close or extra far anytime. Change any key in the menu's Hotkeys section or with /rebind."),
            new GuideSection(
                "Work or Relax",
                "Turn on Performance Overlay, then tap WORK or RELAX beside FPS / PING / RAM. BlueSage Report " +
                "shows how your time split. It's a label just for you; your game stats, streaks and rewards never change."),
            new GuideSection(
                "Chat that's easier to read",
                "Timestamps, name mentions, copy buttons and safer links. If chat ever looks odd, /chatvanilla " +
                "puts the normal chat back, then switch things on again one at a time."),
            new GuideSection(
                "Little pop-ups (Toasts)",
                "Small notes at the top of your screen for hotkeys, cleanups, reconnects, settings and host news, " +
                "so nothing slips past on a small laptop screen. Like it quiet? Turn Toast Pop-ups off in Toggles."),
            new GuideSection(
                "Keeping things smooth",
                "Automatic Cleanup tidies unused stuff every 15 minutes. If you drop out, reconnect waits a moment " +
                "so the host can clear your old spot first; that keeps the player count honest."),
            new GuideSection(
                "Hosts & Helpers",
                "Hosts can add trusted Helpers with /helper add. Hosts and Helpers can send everyone with QoL a quick " +
                "heads-up with /announce <message>, perfect for restarts or breaks. BlueSage checks each player's " +
                "Steam ID with Steam itself, so a look-alike name can't slip past your ban list."),
            new GuideSection(
                "Need a hand?",
                "/qol status runs a quick health check, and typing / lets you browse every command. The QoL menu " +
                "links the community Discord, and our website is Coming Soon. Made by Blue + Sage = BlueSage with superpowered neurodivergence ♥"),
        };
    }
}
