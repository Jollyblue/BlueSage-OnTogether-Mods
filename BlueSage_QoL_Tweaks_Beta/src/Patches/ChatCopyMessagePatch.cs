using System;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch(typeof(TextChannelManager))]
internal static class ChatCopyMessagePatch
{
    private sealed class ChatRowCreationSnapshot
    {
        internal ChatRowCreationSnapshot(
            Transform container,
            int[] beforeInstanceIds)
        {
            Container = container;
            BeforeInstanceIds = beforeInstanceIds ?? Array.Empty<int>();
        }

        internal Transform Container { get; }

        internal int[] BeforeInstanceIds { get; }
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]
    private static void AddMessageUI_Prefix(
        bool isLocal,
        out ChatRowCreationSnapshot __state)
    {
        Transform container = isLocal
            ? MonoSingleton<UIManager>.I?.TextContentLocalTransform
            : MonoSingleton<UIManager>.I?.TextContentGlobalTransform;
        __state = CaptureRows(container);
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddMessageUI", typeof(string), typeof(string), typeof(bool), typeof(int), typeof(bool))]
    private static void AddMessageUI_Postfix(
        string userName,
        string text,
        bool isLocal,
        int senderIndex,
        bool sendByPlayer,
        ChatRowCreationSnapshot __state)
    {
        // `sendByPlayer` distinguishes local echo from received-channel rendering;
        // it does not determine whether the enclosing RPC has an exact sender tuple.
        AuthoritativeChatSenderContext senderContext =
            ChatSenderContextPatch.CurrentSenderContext;
        SessionAuditController.RecordChat(
            userName,
            text,
            isLocal,
            senderIndex,
            senderContext);
        if (!Plugin.ShouldApplyChatCopy)
        {
            return;
        }

        try
        {
            AttachToCreatedRow(__state, text, senderContext);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Chat Copy could not attach click handler: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch("AddNotification", typeof(string))]
    private static void AddNotification_Prefix(
        out ChatRowCreationSnapshot __state)
    {
        __state = CaptureRows(
            MonoSingleton<UIManager>.I?.TextContentGlobalTransform);
    }

    [HarmonyPostfix]
    [HarmonyPatch("AddNotification", typeof(string))]
    private static void AddNotification_Postfix(
        string text,
        ChatRowCreationSnapshot __state)
    {
        SessionAuditController.RecordNotification(text);
        if (!Plugin.ShouldApplyChatCopy)
        {
            return;
        }

        try
        {
            // Build 24141704 AddNotification instantiates one row under Global.
            // BlueSage command responses use this exact local-only lifecycle.
            AttachToCreatedRow(__state, text, null);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Chat Copy could not attach to local notification: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ChatRowCreationSnapshot CaptureRows(
        Transform container)
    {
        if (container == null || !Plugin.ShouldApplyChatCopy)
        {
            return new ChatRowCreationSnapshot(container, Array.Empty<int>());
        }

        int[] instanceIds = new int[container.childCount];
        for (int index = 0; index < container.childCount; index++)
        {
            instanceIds[index] = container.GetChild(index).GetInstanceID();
        }

        return new ChatRowCreationSnapshot(container, instanceIds);
    }

    private static void AttachToCreatedRow(
        ChatRowCreationSnapshot snapshot,
        string sourceText,
        AuthoritativeChatSenderContext senderContext)
    {
        Transform container = snapshot?.Container;
        if (container == null || container.childCount <= 0)
        {
            return;
        }

        int[] afterInstanceIds = new int[container.childCount];
        for (int index = 0; index < container.childCount; index++)
        {
            afterInstanceIds[index] =
                container.GetChild(index).GetInstanceID();
        }

        ChatRowReconciliationDecision decision =
            ChatRowReconciliationPolicy.Resolve(
                snapshot.BeforeInstanceIds,
                afterInstanceIds);
        if (!decision.HasExactCreatedRow)
        {
            return;
        }

        string payload = ChatCopySanitizer.ToClipboardText(sourceText);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        string safeUrl = Plugin.ShouldApplyChatUrlLinks && ChatUrlDetector.TryGetFirstUrl(payload, out string detectedUrl)
            ? detectedUrl
            : string.Empty;

        GameObject messageRow = null;
        for (int index = 0; index < container.childCount; index++)
        {
            Transform child = container.GetChild(index);
            if (child.GetInstanceID() == decision.CreatedRowInstanceId)
            {
                messageRow = child.gameObject;
                break;
            }
        }

        if (messageRow == null)
        {
            return;
        }
        if (!string.IsNullOrWhiteSpace(safeUrl))
        {
            ApplyLinkVisuals(messageRow);
        }

        GameObject interactionSurface = ResolveInteractionSurface(messageRow);
        ChatMessageInteractionController handler =
            interactionSurface.GetComponent<ChatMessageInteractionController>() ??
            interactionSurface.AddComponent<ChatMessageInteractionController>();
        handler.Initialize(payload, safeUrl, senderContext);
    }

    private static GameObject ResolveInteractionSurface(GameObject messageRow)
    {
        if (messageRow == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours =
            messageRow.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null ||
                behaviour is ChatMessageInteractionController ||
                !(behaviour is IPointerClickHandler))
            {
                continue;
            }

            Graphic graphic = behaviour.GetComponent<Graphic>();
            if (graphic != null && graphic.raycastTarget)
            {
                return behaviour.gameObject;
            }
        }

        return messageRow;
    }

    private static void ApplyLinkVisuals(GameObject messageRow)
    {
        if (messageRow == null)
        {
            return;
        }

        TMP_Text[] labels = messageRow.GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text label in labels)
        {
            if (label != null && ChatLinkFormatter.TryColorizeUrls(label.text, out string formatted))
            {
                label.text = formatted;
            }
        }
    }
}
