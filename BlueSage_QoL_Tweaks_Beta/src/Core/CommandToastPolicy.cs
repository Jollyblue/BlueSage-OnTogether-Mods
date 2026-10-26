#nullable enable
using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

// Round 5h (Blue, 2026-10-25): chat commands that change something answer with a toast as well as the chat line, the way
// the matching switch in the QoL menu already does. Commands that only show information stay in chat, so a status
// listing never sprays pop-ups. Every registered command is filed in one of the sets below (a test counts them).
internal static class CommandToastPolicy
{
    // Change a setting, your name or status, or open a window: the first result line is also shown as a toast.
    private static readonly HashSet<string> Toasting = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bsqol", "fishaid", "rebind", "welcome", "hosthealth", "leavenotices", "timestamps", "chatreadability",
        "chatbackdrop", "chatoutline", "chatscale", "chatfont", "chatheight", "chatrows", "chatvanilla",
        "chatoutlinecolor", "blackoutlinecolor", "bettermove", "unlimitedconsumables", "reconnectguard",
        "reconnectmessage", "focusanywhere", "minimaplabels", "cloneshield", "playersaver", "copychat", "chatlinks",
        "auditlog", "qolmenu", "selfmove", "style", "styleui", "setpingcolor", "pingsound", "helper", "setname",
        "status", "brb", "afk", "back", "clearstatus", "statuscolor", "spoons", "setmood",
        "workon", "relaxon", "workoff", "relaxoff", "mode"
    };

    // A preview stays in chat; the confirmed action toasts its result.
    private static readonly HashSet<string> ToastWhenConfirmed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sidkick", "sidmute", "sidunmute", "sidban", "sidunban"
    };

    // Information, listings, internal commands, or commands that already raise their own toast.
    private static readonly HashSet<string> Quiet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "motd", "announce", "assetsweep", "communitybans", "chalk", "chalksave", "chalkload", "chalklist", "sidlist",
        "sidincident", "sidwho", "idsync", "boop", "sagewave", "bluedevdebug"
    };

    // First words that mean "just tell me", not "change it".
    private static readonly HashSet<string> InformationWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "status", "?", "list", "help", "folder", "check", "path"
    };

    // With no argument these only show the current value (a read); they toast when they are given a value.
    private static readonly HashSet<string> BareIsRead = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chatscale", "chatfont", "chatheight", "chatrows", "chatoutline", "chatoutlinecolor", "blackoutlinecolor",
        "setname", "status", "statuscolor", "spoons", "selfmove", "helper", "pingsound", "setpingcolor", "setmood",
        "auditlog", "rebind"
    };

    // Their argument is free text (a name, a message, a colour), so the first word is a value, never an information word.
    private static readonly HashSet<string> FreeText = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "setname", "status", "statuscolor", "setmood"
    };

    internal static int ToastingCommandCount => Toasting.Count + ToastWhenConfirmed.Count;

    internal static int QuietCommandCount => Quiet.Count;

    internal static bool ShouldToastFirstMessage(string? commandName, IReadOnlyList<string>? tokens)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return false;
        }

        string name = commandName!.Trim().TrimStart('/');
        string first = tokens != null && tokens.Count > 0 ? (tokens[0] ?? string.Empty).Trim() : string.Empty;
        if (ToastWhenConfirmed.Contains(name))
        {
            // The grammar is "/sidban <id> confirm": the id first, "confirm" last. A bare "confirm" is refused by the command.
            return tokens != null && tokens.Count >= 2 &&
                   string.Equals((tokens[tokens.Count - 1] ?? string.Empty).Trim(), "confirm", StringComparison.OrdinalIgnoreCase);
        }

        if (!Toasting.Contains(name))
        {
            return false;
        }

        if (first.Length == 0)
        {
            return !BareIsRead.Contains(name);
        }

        return FreeText.Contains(name) || !InformationWords.Contains(first);
    }
}
