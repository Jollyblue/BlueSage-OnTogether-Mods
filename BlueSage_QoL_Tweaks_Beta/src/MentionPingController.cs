using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using TMPro;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class MentionPingController : MonoBehaviour
{
    private const float AutocompleteCooldownSeconds = 0.15f;
    private const int MaxSuggestions = 5;
    private float _lastAutocompleteAt = -1f;
    private IReadOnlyList<string> _suggestions = Array.Empty<string>();
    private int _mentionStart = -1;
    private int _mentionCaret = -1;
    private string _mentionQuery = string.Empty;
    private string _lastObservedInputText = string.Empty;
    private int _lastObservedCaret = -1;
    private GUIStyle _popupStyle;
    private GUIStyle _popupHeaderStyle;
    private GUIStyle _popupButtonStyle;
    private string _lastThemeKey = string.Empty;
    private int _renderedTextureGeneration;
    private bool _lastLobbyPingAccess;

    private void Update()
    {
        if (Plugin.EnablePingMentions == null || !Plugin.EnablePingMentions.Value)
        {
            ClearSuggestions();
            _lastObservedInputText = string.Empty;
            _lastObservedCaret = -1;
            return;
        }

        TMP_InputField input = GetChatInput();
        if (input == null || !input.isFocused)
        {
            ClearSuggestions();
            _lastObservedInputText = string.Empty;
            _lastObservedCaret = -1;
            return;
        }

        string currentText = input.text ?? string.Empty;
        int currentCaret = input.caretPosition;
        bool lobbyPingAccess = Plugin.CanUseLobbySafety(out _);
        if (!string.Equals(currentText, _lastObservedInputText, StringComparison.Ordinal) || currentCaret != _lastObservedCaret ||
            lobbyPingAccess != _lastLobbyPingAccess)
        {
            _lastObservedInputText = currentText;
            _lastObservedCaret = currentCaret;
            _lastLobbyPingAccess = lobbyPingAccess;
            RefreshSuggestions(input);
        }

        if (!Input.GetKeyDown(KeyCode.Tab) || Time.unscaledTime - _lastAutocompleteAt < AutocompleteCooldownSeconds)
        {
            return;
        }

        if (TryAutocomplete(input, _suggestions.Count > 0 ? _suggestions[0] : string.Empty))
        {
            _lastAutocompleteAt = Time.unscaledTime;
        }
    }

    private void OnGUI()
    {
        if (_suggestions == null || _suggestions.Count == 0)
        {
            return;
        }

        EnsurePopupStyles();
        float width = 340f;
        float height = 44f + (_suggestions.Count * 30f);
        Rect rect = new Rect(36f, Mathf.Max(36f, Screen.height - height - 118f), width, height);
        GUILayout.BeginArea(rect, _popupStyle);
        GUILayout.Label("BlueSage @ mention suggestions - Tab completes the first match", _popupHeaderStyle);
        for (int index = 0; index < _suggestions.Count; index++)
        {
            string suggestion = _suggestions[index];
            string label = suggestion.Equals("lobby", StringComparison.OrdinalIgnoreCase)
                ? "@lobby - ping everyone using compatible ping mods"
                : "@" + suggestion;
            if (GUILayout.Button(new GUIContent(label, "Click or press Tab to complete the mention."), _popupButtonStyle, GUILayout.Height(26f)))
            {
                TMP_InputField input = GetChatInput();
                if (input != null)
                {
                    TryAutocomplete(input, suggestion);
                }
            }
        }
        GUILayout.EndArea();
    }

    public static void PlayConfiguredPing()
    {
        if (Plugin.EnablePingSound == null || !Plugin.EnablePingSound.Value)
        {
            return;
        }

        try
        {
            SFXManager sfxManager = MonoSingleton<SFXManager>.I;
            if (sfxManager != null)
            {
                switch (Plugin.GetPingSoundMode())
                {
                    case "change":
                        sfxManager.PlayUIChange();
                        break;
                    case "error":
                        sfxManager.PlayUIError();
                        break;
                    case "task":
                        sfxManager.PlayTaskComplete();
                        break;
                    case "ticket":
                        sfxManager.PlayEarnTicket();
                        break;
                    default:
                        sfxManager.PlayUIClick();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"Ping Mentions sound skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static TMP_InputField GetChatInput()
    {
        try
        {
            return MonoSingleton<UIManager>.I?.MessageInput;
        }
        catch
        {
            return null;
        }
    }

    private void RefreshSuggestions(TMP_InputField input)
    {
        if (!TryFindMentionContext(input, out int atIndex, out int caret, out string query))
        {
            ClearSuggestions();
            return;
        }

        _mentionStart = atIndex;
        _mentionCaret = caret;
        _mentionQuery = query;
        _suggestions = PlayerMentionProvider.FindMentionMatches(query, MaxSuggestions);
    }

    private static bool TryAutocomplete(TMP_InputField input, string preferredMatch)
    {
        if (!TryFindMentionContext(input, out int atIndex, out int caret, out string query))
        {
            return false;
        }

        string match = string.IsNullOrWhiteSpace(preferredMatch)
            ? PlayerMentionProvider.FindBestMentionMatch(query)
            : preferredMatch;
        if (string.IsNullOrWhiteSpace(match))
        {
            return false;
        }

        if (string.Equals(match, "lobby", StringComparison.OrdinalIgnoreCase) && !Plugin.CanUseLobbySafety(out _))
        {
            return false;
        }

        string cleanMatch = MentionNamePolicy.ToMentionInsertionToken(match);
        if (string.IsNullOrWhiteSpace(cleanMatch))
        {
            return false;
        }

        string text = input.text ?? string.Empty;
        string before = text.Substring(0, atIndex);
        string after = text.Substring(caret);
        string replacement = "@" + cleanMatch + " ";
        input.text = before + replacement + after;
        input.caretPosition = before.Length + replacement.Length;
        input.Select();
        input.ActivateInputField();
        return true;
    }

    private static bool TryFindMentionContext(TMP_InputField input, out int atIndex, out int caret, out string query)
    {
        atIndex = -1;
        caret = -1;
        query = string.Empty;

        string text = input.text ?? string.Empty;
        caret = Math.Max(0, Math.Min(input.caretPosition, text.Length));
        if (caret <= 0)
        {
            return false;
        }

        atIndex = text.LastIndexOf('@', caret - 1);
        if (atIndex < 0)
        {
            return false;
        }

        if (atIndex > 0 && !char.IsWhiteSpace(text[atIndex - 1]))
        {
            return false;
        }

        query = text.Substring(atIndex + 1, caret - atIndex - 1);
        return !string.IsNullOrWhiteSpace(query) && query.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0;
    }

    private void ClearSuggestions()
    {
        _suggestions = Array.Empty<string>();
        _mentionStart = -1;
        _mentionCaret = -1;
        _mentionQuery = string.Empty;
    }

    private void EnsurePopupStyles()
    {
        BlueSageUiThemePalette theme = BlueSageUiTheme.Current;
        if (_popupStyle != null &&
            string.Equals(_lastThemeKey, theme.Key, StringComparison.Ordinal) &&
            _renderedTextureGeneration == BlueSageUiTheme.RuntimeTextureGeneration &&
            BlueSageUiTheme.AreRuntimeTexturesAlive())
        {
            return;
        }

        _lastThemeKey = theme.Key;
        _popupStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 10),
            normal = { background = BlueSageUiTheme.GetSolidTexture(theme.PreviewBackground), textColor = theme.PreviewText }
        };
        _popupHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = theme.PopupHeaderText }
        };
        _popupButtonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, theme, 13);
        _popupButtonStyle.alignment = TextAnchor.MiddleLeft;
        _renderedTextureGeneration = BlueSageUiTheme.RuntimeTextureGeneration;
    }
}
