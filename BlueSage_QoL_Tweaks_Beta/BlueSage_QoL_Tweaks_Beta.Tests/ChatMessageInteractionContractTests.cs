using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChatMessageInteractionContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Every_bounded_player_message_captures_exact_rpc_sender_context_including_row_zero()
    {
        string source = Read("src", "Patches", "ChatSenderContextPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("RPCInfo info"));
            Assert.That(source, Does.Contain("[ThreadStatic]"));
            Assert.That(source, Does.Contain("CurrentSenderContext"));
            Assert.That(source, Does.Contain("MaximumChatPayloadBytes"));
            Assert.That(source, Does.Contain("AuthoritativeChatSenderPolicy.TryResolveUniqueIndex("));
            Assert.That(source, Does.Contain("info.sender"));
            Assert.That(source, Does.Contain("panel.PlayerIDs"));
            Assert.That(source, Does.Contain("PlayerItemControllers[senderIndex]"));
            Assert.That(source, Does.Contain("PlayerTransforms[senderIndex]"));
            Assert.That(source, Does.Not.Contain("ContainsLobbyPingToken"));
            Assert.That(source, Does.Not.Contain("info.sender.Equals(default(PlayerID))"));
            Assert.That(source, Does.Not.Contain("senderIndex > 0"));
        });
    }

    [Test]
    public void Five_argument_row_binding_receives_sender_tuple_synchronously_and_notifications_receive_none()
    {
        string patch = Read("src", "Patches", "ChatCopyMessagePatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                patch,
                Does.Contain("[HarmonyPatch(\"AddMessageUI\", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]"));
            Assert.That(patch, Does.Contain("ChatSenderContextPatch.CurrentSenderContext"));
            Assert.That(patch, Does.Contain("SessionAuditController.RecordChat"));
            Assert.That(patch, Does.Contain("ChatMessageInteractionController"));
            Assert.That(patch, Does.Contain("AddMessageUI_Prefix("));
            Assert.That(patch, Does.Contain("out ChatRowCreationSnapshot __state"));
            Assert.That(patch, Does.Contain("AttachToCreatedRow(__state, text, senderContext)"));
            Assert.That(patch, Does.Contain("AttachToCreatedRow(__state, text, null)"));
            Assert.That(patch, Does.Contain("ChatRowReconciliationPolicy.Resolve("));
            Assert.That(patch, Does.Not.Contain("container.childCount - 1"));
        });
    }

    [Test]
    public void Remote_channel_rows_keep_the_exact_rpc_sender_context_even_when_send_by_player_is_false()
    {
        string patch = Read("src", "Patches", "ChatCopyMessagePatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                patch,
                Does.Match(
                    @"AuthoritativeChatSenderContext\s+senderContext\s*=\s*" +
                    @"ChatSenderContextPatch\.CurrentSenderContext;"));
            Assert.That(
                patch,
                Does.Not.Match(
                    @"sendByPlayer\s*\?\s*" +
                    @"ChatSenderContextPatch\.CurrentSenderContext"));
        });
    }

    [Test]
    public void Chat_actions_share_the_new_vanilla_pointer_surface_and_keep_a_root_fallback()
    {
        string patch = Read("src", "Patches", "ChatCopyMessagePatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("ResolveInteractionSurface(messageRow)"));
            Assert.That(patch, Does.Contain("IPointerClickHandler"));
            Assert.That(patch, Does.Contain("graphic.raycastTarget"));
            Assert.That(patch, Does.Contain("return messageRow;"));
        });
    }

    [Test]
    public void Click_runtime_uses_pure_policy_revalidates_native_tuple_and_never_consumes_vanilla_events()
    {
        string source = Read("src", "Patches", "ChatMessageInteractionController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("ChatClickActionPolicy.Decide"));
            Assert.That(source, Does.Contain("NativeIdCardController.TryResolveCurrentRow"));
            Assert.That(source, Does.Contain("NativeIdCardController.TryOpen"));
            Assert.That(source, Does.Contain("ScopedSenderEvidencePolicy.TryBuild"));
            Assert.That(source, Does.Contain("SessionRowCorrelator"));
            Assert.That(source, Does.Not.Contain("eventData.Use"));
            Assert.That(source, Does.Not.Contain("DisplayName"));
            Assert.That(source, Does.Not.Contain("senderIndex > 0"));
        });
    }

    [Test]
    public void Local_and_remote_rows_keep_plain_left_vanilla_and_route_modifier_left_to_copy_policy()
    {
        string source = Read("src", "Patches", "ChatMessageInteractionController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("eventData.button == PointerEventData.InputButton.Left"));
            Assert.That(source, Does.Contain("HasAnyModifierHeld()"));
            Assert.That(source, Does.Contain("HandleClick(isRightButton: false)"));
            Assert.That(source, Does.Not.Contain("_nativeButton == null &&"));
            Assert.That(source, Does.Not.Contain("eventData.Use"));
        });
    }

    [Test]
    public void Authorized_audit_uses_the_captured_exact_tuple_not_name_or_row_index_authority()
    {
        string audit = Read("src", "SessionAuditController.cs");
        string method = Slice(
            audit,
            "internal static void RecordChat(",
            "internal static void RecordNotification(");

        Assert.Multiple(() =>
        {
            Assert.That(method, Does.Contain("AuthoritativeChatSenderContext senderContext"));
            Assert.That(method, Does.Contain("senderContext?.ExactTupleCaptured"));
            Assert.That(method, Does.Not.Contain("TryResolveRosterIndex"));
            Assert.That(method, Does.Not.Contain("senderIndex > 0"));
            Assert.That(method, Does.Not.Contain("TryResolveLookup(displayNameRaw"));
        });
    }

    [Test]
    public void Public_runtime_messages_never_render_or_log_exact_sender_identifiers()
    {
        string interaction = Read("src", "Patches", "ChatMessageInteractionController.cs");
        string senderCapture = Read("src", "Patches", "ChatSenderContextPatch.cs");

        Assert.Multiple(() =>
        {
            Assert.That(interaction, Does.Not.Contain("\"SteamID64: \" +"));
            Assert.That(interaction, Does.Not.Contain("\"PurrNet PlayerID: \" +"));
            Assert.That(interaction, Does.Not.Contain("Plugin.Log?.LogInfo(_sender"));
            Assert.That(interaction, Does.Not.Contain("Plugin.Log?.LogWarning(_sender"));
            Assert.That(senderCapture, Does.Not.Contain("senderSteamId +"));
            Assert.That(senderCapture, Does.Not.Contain("senderPlayerId +"));
        });
    }

    [Test]
    public void Denied_chat_row_actions_fail_closed_in_local_logs_without_creating_chat_rows()
    {
        string interaction = Read("src", "Patches", "ChatMessageInteractionController.cs");
        string denied = Slice(interaction, "private static void NotifyDenied(", "private static void PlayClickCue()");

        Assert.Multiple(() =>
        {
            Assert.That(denied, Does.Contain("Plugin.Log?.LogWarning("));
            Assert.That(denied, Does.Contain("ChatActionDenialLogPolicy.CategorizeReason(reason)"));
            Assert.That(denied, Does.Contain("\"Chat row action stayed locked: \" + safeReason"));
            Assert.That(denied, Does.Not.Contain("AddLocalNotification"));
            Assert.That(denied, Does.Not.Contain("FormatBlueSageAction"));
            Assert.That(denied, Does.Not.Contain("_sender"));
            Assert.That(denied, Does.Not.Contain("SteamId"));
            Assert.That(denied, Does.Not.Contain("PlayerId"));
            Assert.That(denied, Does.Not.Contain("SteamID"));
            Assert.That(denied, Does.Not.Contain("PlayerID"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(Root, Path.Combine(parts)));

    private static string Slice(string source, string start, string end)
    {
        int startIndex = source.IndexOf(start, StringComparison.Ordinal);
        int endIndex = source.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), "Missing start marker: " + start);
        Assert.That(endIndex, Is.GreaterThan(startIndex), "Missing end marker: " + end);
        return source.Substring(startIndex, endIndex - startIndex);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(candidate))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the QoL project root.");
    }
}
