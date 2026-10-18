namespace BlueSage.QoLTweaks.Core;

internal enum ChatClickAction
{
    Ignore,
    CopyMessage,
    GuardedSafeLink,
    CopyScopedIdentityEvidence,
    OpenNativeIdCard,
    Denied,
}

internal sealed class ChatClickContext
{
    internal bool IsRightButton { get; set; }
    internal bool ShiftHeld { get; set; }
    internal bool ControlHeld { get; set; }
    internal bool AltHeld { get; set; }
    internal bool HasPayload { get; set; }
    internal bool LinkOpeningEnabled { get; set; }
    internal bool HasSafeUrl { get; set; }
    internal bool HasSenderBinding { get; set; }
}

internal readonly struct ChatClickDecision
{
    internal ChatClickDecision(ChatClickAction action, string reason)
    {
        Action = action;
        Reason = reason ?? string.Empty;
    }

    internal ChatClickAction Action { get; }
    internal string Reason { get; }
}

internal static class ChatClickActionPolicy
{
    internal static ChatClickDecision Decide(ChatClickContext context)
    {
        if (context == null)
        {
            return new ChatClickDecision(ChatClickAction.Ignore, "no click context");
        }

        int modifiers =
            (context.ShiftHeld ? 1 : 0) +
            (context.ControlHeld ? 1 : 0) +
            (context.AltHeld ? 1 : 0);
        if (modifiers > 1)
        {
            return Denied("use exactly one modifier");
        }

        if (!context.IsRightButton)
        {
            if (context.ControlHeld)
            {
                return context.HasPayload
                    ? new ChatClickDecision(
                        ChatClickAction.CopyMessage,
                        "copy the bounded message from the native button path")
                    : Denied("the row has no bounded copy payload");
            }

            if (context.ShiftHeld)
            {
                return context.HasPayload &&
                       context.LinkOpeningEnabled &&
                       context.HasSafeUrl
                    ? new ChatClickDecision(
                        ChatClickAction.GuardedSafeLink,
                        "use the two-step safe-link path from the native button")
                    : Denied("this row has no enabled safe HTTP(S) link");
            }

            return context.HasSenderBinding
                ? new ChatClickDecision(
                    ChatClickAction.OpenNativeIdCard,
                    "override the native listener with the fresh exact sender tuple")
                : new ChatClickDecision(
                    ChatClickAction.Ignore,
                    "plain left click without an exact sender binding remains owned by the game");
        }

        if (!context.HasPayload)
        {
            return Denied("the row has no bounded copy payload");
        }

        if (context.ControlHeld)
        {
            return context.HasSenderBinding
                ? new ChatClickDecision(
                    ChatClickAction.CopyScopedIdentityEvidence,
                    "fresh exact sender revalidation is required")
                : Denied("this row has no exact player-sender binding");
        }

        if (context.AltHeld)
        {
            return context.HasSenderBinding
                ? new ChatClickDecision(
                    ChatClickAction.OpenNativeIdCard,
                    "fresh exact sender revalidation is required")
                : Denied("this row has no exact player-sender binding");
        }

        if (context.ShiftHeld)
        {
            return context.LinkOpeningEnabled && context.HasSafeUrl
                ? new ChatClickDecision(
                    ChatClickAction.GuardedSafeLink,
                    "use the two-step safe-link path")
                : Denied("this row has no enabled safe HTTP(S) link");
        }

        return new ChatClickDecision(
            ChatClickAction.CopyMessage,
            "copy only the bounded message payload");
    }

    private static ChatClickDecision Denied(string reason)
    {
        return new ChatClickDecision(ChatClickAction.Denied, reason);
    }
}
