using System;
using BlueSage.QoLTweaks.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

internal sealed class ChatMessageInteractionController :
    MonoBehaviour,
    IPointerClickHandler
{
    private const float ClickCooldownSeconds = 0.25f;
    private const float LinkConfirmationSeconds = 10f;

    private string _payload = string.Empty;
    private string _safeUrl = string.Empty;
    private AuthoritativeChatSenderContext _sender;
    private float _lastActionTime;
    private string _pendingOpenUrl = string.Empty;
    private float _pendingOpenUntil;
    private Button _nativeButton;
    private bool _nativeButtonListenerRegistered;

    internal void Initialize(
        string payload,
        string safeUrl,
        AuthoritativeChatSenderContext sender)
    {
        _payload = payload ?? string.Empty;
        _safeUrl = safeUrl ?? string.Empty;
        _sender = sender?.ExactTupleCaptured == true ? sender : null;
        _pendingOpenUrl = string.Empty;
        _pendingOpenUntil = 0f;
        EnsureRaycastSurface();
        RegisterNativeButtonClick();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            HandleClick(isRightButton: true);
            return;
        }

        // The game creates the same Button-backed row for local and remote
        // messages, but only remote rows receive its native listener. Route
        // modifier-left directly for both paths. The action cooldown collapses
        // the duplicate callback on remote rows while plain left stays vanilla.
        if (eventData.button == PointerEventData.InputButton.Left &&
            HasAnyModifierHeld())
        {
            HandleClick(isRightButton: false);
        }
    }

    private void HandleNativeButtonClick()
    {
        HandleClick(isRightButton: false);
    }

    private static bool HasAnyModifierHeld()
    {
        return Input.GetKey(KeyCode.LeftShift) ||
               Input.GetKey(KeyCode.RightShift) ||
               Input.GetKey(KeyCode.LeftControl) ||
               Input.GetKey(KeyCode.RightControl) ||
               Input.GetKey(KeyCode.LeftAlt) ||
               Input.GetKey(KeyCode.RightAlt);
    }

    private void HandleClick(bool isRightButton)
    {
        bool shiftHeld =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);
        bool controlHeld =
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl);
        bool altHeld =
            Input.GetKey(KeyCode.LeftAlt) ||
            Input.GetKey(KeyCode.RightAlt);
        ChatClickDecision decision = ChatClickActionPolicy.Decide(
            new ChatClickContext
            {
                IsRightButton = isRightButton,
                ShiftHeld = shiftHeld,
                ControlHeld = controlHeld,
                AltHeld = altHeld,
                HasPayload = !string.IsNullOrWhiteSpace(_payload),
                LinkOpeningEnabled = Plugin.ShouldApplyChatUrlLinks,
                HasSafeUrl = !string.IsNullOrWhiteSpace(_safeUrl),
                HasSenderBinding = _sender?.ExactTupleCaptured == true,
            });

        if (decision.Action == ChatClickAction.Ignore)
        {
            return;
        }

        if (!Plugin.ShouldApplyChatCopy ||
            Time.unscaledTime - _lastActionTime < ClickCooldownSeconds)
        {
            return;
        }

        _lastActionTime = Time.unscaledTime;
        switch (decision.Action)
        {
            case ChatClickAction.CopyMessage:
                CopyMessage();
                return;
            case ChatClickAction.GuardedSafeLink:
                HandleGuardedSafeLink();
                return;
            case ChatClickAction.CopyScopedIdentityEvidence:
                CopyScopedIdentityEvidence();
                return;
            case ChatClickAction.OpenNativeIdCard:
                OpenNativeIdCard();
                return;
            case ChatClickAction.Denied:
                NotifyDenied(decision.Reason);
                return;
            default:
                return;
        }
    }

    private void CopyMessage()
    {
        GUIUtility.systemCopyBuffer = _payload;
        PlayClickCue();
        Plugin.AddLocalNotification(
            string.IsNullOrWhiteSpace(_safeUrl)
                ? Plugin.FormatBlueSageAction("Copied", "chat text.")
                : Plugin.FormatBlueSageAction(
                    "Copied",
                    "chat text. Shift-left-click once copies/warns; " +
                    "Shift-left-click again opens the link.",
                    warning: true));
    }

    private void HandleGuardedSafeLink()
    {
        if (string.Equals(
                _pendingOpenUrl,
                _safeUrl,
                StringComparison.OrdinalIgnoreCase) &&
            Time.unscaledTime <= _pendingOpenUntil)
        {
            Application.OpenURL(_safeUrl);
            _pendingOpenUrl = string.Empty;
            _pendingOpenUntil = 0f;
            PlayClickCue();
            Plugin.AddLocalNotification(
                Plugin.FormatBlueSageAction(
                    "Opening chat link",
                    "Be careful with random links.",
                    warning: true));
            return;
        }

        GUIUtility.systemCopyBuffer = _safeUrl;
        _pendingOpenUrl = _safeUrl;
        _pendingOpenUntil =
            Time.unscaledTime + LinkConfirmationSeconds;
        PlayClickCue();
        Plugin.AddLocalNotification(
                Plugin.FormatBlueSageAction(
                    "Link copied",
                "Be careful with random links. Shift-left-click the same " +
                "link again within 10s to open.",
                warning: true));
    }

    private void CopyScopedIdentityEvidence()
    {
        if (!TryResolveCurrentSender(out NativeIdCardTarget target))
        {
            NotifyDenied(
                "the exact sender tuple changed; use the native player panel " +
                "and a fresh local audit snapshot");
            return;
        }

        LobbySafetyAccessState access =
            SteamIdModerationController.GetLocalAccessState(out _, out _);
        bool privileged =
            access == LobbySafetyAccessState.Host ||
            access == LobbySafetyAccessState.HelperReady;
        var context = new ScopedSenderEvidenceContext
        {
            HasSenderBinding = _sender.ExactTupleCaptured,
            UniqueCurrentAgreement = true,
            FreshAtClick = true,
            ExactTupleMatches = ExactTupleMatches(target),
            PrivilegedObserver = privileged,
            SteamId = target.SteamId,
            PlayerId = target.PlayerId,
            NativeRowIndex = target.PlayerIndex,
            SessionRowCorrelator = _sender.SessionRowCorrelator,
        };
        if (!ScopedSenderEvidencePolicy.TryBuild(
                context,
                out string evidence,
                out string error))
        {
            NotifyDenied(error);
            return;
        }

        GUIUtility.systemCopyBuffer = evidence;
        PlayClickCue();
        Plugin.AddLocalNotification(
            Plugin.FormatBlueSageAction(
                "Player details copied",
                "For reference only. Check the player in the player list " +
                "before taking any action.",
                warning: true));
    }

    private void OpenNativeIdCard()
    {
        if (_sender == null)
        {
            NotifyDenied("this row has no exact player-sender binding");
            return;
        }

        if (!NativeIdCardController.TryOpen(
                "chat-row",
                _sender.SteamId,
                _sender.NativeRowIndex,
                _sender.PlayerId,
                _sender.Row,
                _sender.NetworkTransform,
                requireObservedIndex: true,
                CurrentCallerKind()))
        {
            NotifyDenied(
                "the exact sender tuple changed; reopen the current player " +
                "panel before trying again");
            return;
        }

        PlayClickCue();
    }

    private bool TryResolveCurrentSender(out NativeIdCardTarget target)
    {
        target = null;
        return _sender != null &&
               NativeIdCardController.TryResolveCurrentRow(
                   "chat-evidence",
                   _sender.SteamId,
                   _sender.NativeRowIndex,
                   _sender.PlayerId,
                   _sender.Row,
                   _sender.NetworkTransform,
                   requireObservedIndex: true,
                   CurrentCallerKind(),
                   out target,
                   out _) &&
               ExactTupleMatches(target);
    }

    private bool ExactTupleMatches(NativeIdCardTarget target)
    {
        return _sender != null &&
               target != null &&
               target.PlayerIndex == _sender.NativeRowIndex &&
               string.Equals(
                   target.SteamId,
                   _sender.SteamId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   target.PlayerId,
                   _sender.PlayerId,
                   StringComparison.Ordinal) &&
               ReferenceEquals(target.Row, _sender.Row) &&
               ReferenceEquals(
                   target.NetworkTransform,
                   _sender.NetworkTransform);
    }

    private static NativeIdCardCallerKind CurrentCallerKind()
    {
        LobbySafetyAccessState access =
            SteamIdModerationController.GetLocalAccessState(out _, out _);
        if (access == LobbySafetyAccessState.Host)
        {
            return NativeIdCardCallerKind.Host;
        }

        return access == LobbySafetyAccessState.HelperReady
            ? NativeIdCardCallerKind.AuthenticatedHelper
            : NativeIdCardCallerKind.OrdinaryUser;
    }

    private static void NotifyDenied(string reason)
    {
        string safeReason = ChatActionDenialLogPolicy.CategorizeReason(reason);
        Plugin.Log?.LogWarning(
            "Chat row action stayed locked: " + safeReason + ".");
    }

    private static void PlayClickCue()
    {
        try
        {
            MonoSingleton<SFXManager>.I?.PlayUIClick();
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning(
                "Chat row click cue skipped safely: " +
                ex.GetType().Name + ".");
        }
    }

    private void EnsureRaycastSurface()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic == null)
        {
            Image image = gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.001f);
            image.raycastTarget = true;
            return;
        }

        graphic.raycastTarget = true;
    }

    private void RegisterNativeButtonClick()
    {
        if (_nativeButtonListenerRegistered)
        {
            return;
        }

        Button button = GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        _nativeButton = button;
        button.onClick.AddListener(HandleNativeButtonClick);
        _nativeButtonListenerRegistered = true;
    }

    private void OnDestroy()
    {
        if (_nativeButtonListenerRegistered && _nativeButton != null)
        {
            _nativeButton.onClick.RemoveListener(HandleNativeButtonClick);
        }

        _nativeButton = null;
        _nativeButtonListenerRegistered = false;
    }
}
