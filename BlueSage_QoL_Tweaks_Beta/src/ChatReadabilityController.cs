using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Patches;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class ChatReadabilityController : MonoBehaviour
{
    private const float DestroyedStatePruneIntervalSeconds = 5f;
    private bool _lastReadability;
    private bool _lastBackdrop;
    private int _lastChatScalePercent;
    private int _lastChatFontPercent;
    private int _lastChatHeightPercent;
    private bool _lastMessagePanelHidden;
    private bool _lastMessagePanelActivated;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private int _lastRefreshRequestVersion;
    private float _nextDestroyedStatePruneAt;

    private void Update()
    {
        bool readability = Plugin.ShouldApplyChatReadability;
        bool backdrop = Plugin.ShouldApplyPersistentChatBackdrop;
        int chatScalePercent = Plugin.LockedChatUiScalePercent;
        int chatFontPercent = Plugin.LockedChatFontSizePercent;
        int chatHeightPercent = Plugin.LockedChatWindowHeightPercent;
        bool shouldWatch =
            readability ||
            backdrop ||
            chatScalePercent != ChatAccessibilityScalePolicy.DefaultScalePercent ||
            chatFontPercent != ChatFontSizePolicy.DefaultFontSizePercent ||
            chatHeightPercent != ChatWindowHeightPolicy.DefaultHeightPercent ||
            _lastReadability ||
            _lastBackdrop ||
            _lastChatScalePercent != ChatAccessibilityScalePolicy.DefaultScalePercent ||
            _lastChatFontPercent != ChatFontSizePolicy.DefaultFontSizePercent ||
            _lastChatHeightPercent != ChatWindowHeightPolicy.DefaultHeightPercent;
        if (!shouldWatch)
        {
            if (Time.unscaledTime >= _nextDestroyedStatePruneAt)
            {
                _nextDestroyedStatePruneAt = Time.unscaledTime + DestroyedStatePruneIntervalSeconds;
                ChatReadabilityPatch.PruneDestroyedState();
            }
            return;
        }

        if (Time.unscaledTime >= _nextDestroyedStatePruneAt)
        {
            _nextDestroyedStatePruneAt = Time.unscaledTime + DestroyedStatePruneIntervalSeconds;
            ChatReadabilityPatch.PruneDestroyedState();
        }

        UIManager ui = MonoSingleton<UIManager>.I;
        bool panelHidden = ui != null && ui.IsMessagePanelHidden;
        bool panelActivated = ui != null && ui.IsMessagePanelActivated;
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        int refreshRequestVersion = ChatReadabilityPatch.RefreshRequestVersion;
        bool stateChanged =
            readability != _lastReadability ||
            backdrop != _lastBackdrop ||
            chatScalePercent != _lastChatScalePercent ||
            chatFontPercent != _lastChatFontPercent ||
            chatHeightPercent != _lastChatHeightPercent ||
            panelHidden != _lastMessagePanelHidden ||
            panelActivated != _lastMessagePanelActivated ||
            screenWidth != _lastScreenWidth ||
            screenHeight != _lastScreenHeight ||
            refreshRequestVersion != _lastRefreshRequestVersion;

        if (!stateChanged)
        {
            return;
        }
        _lastReadability = readability;
        _lastBackdrop = backdrop;
        _lastChatScalePercent = chatScalePercent;
        _lastChatFontPercent = chatFontPercent;
        _lastChatHeightPercent = chatHeightPercent;
        _lastMessagePanelHidden = panelHidden;
        _lastMessagePanelActivated = panelActivated;
        _lastScreenWidth = screenWidth;
        _lastScreenHeight = screenHeight;
        _lastRefreshRequestVersion = refreshRequestVersion;

        ChatReadabilityPatch.RefreshVisibleChat();
    }
}
