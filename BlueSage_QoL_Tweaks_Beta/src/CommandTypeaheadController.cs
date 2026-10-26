using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class CommandTypeaheadController : MonoBehaviour
{
    private const float AutocompleteCooldownSeconds = 0.15f;
    private const int MaxSuggestions = 7;
    private const int MaxBrowserSuggestions = 64;
    private const int MaxVisibleSuggestions = 8;
    private float _lastAutocompleteAt = -1f;
    private string _lastObservedInputText = string.Empty;
    private int _lastObservedCaret = -1;
    private IReadOnlyList<CommandSuggestion> _suggestions = Array.Empty<CommandSuggestion>();
    private Vector2 _scrollPosition;
    private GUIStyle _popupStyle;
    private GUIStyle _popupHeaderStyle;
    private GUIStyle _popupButtonStyle;
    private GUIStyle _popupHintStyle;
    private Texture2D _popupBackgroundTexture;
    private string _lastThemeKey = string.Empty;
    private int _renderedTextureGeneration;

    private static readonly Dictionary<string, IReadOnlyList<CommandSuggestion>> ArgumentSuggestions =
        new Dictionary<string, IReadOnlyList<CommandSuggestion>>(StringComparer.OrdinalIgnoreCase)
        {
            ["workon"] = Array.Empty<CommandSuggestion>(),
            ["relaxon"] = Array.Empty<CommandSuggestion>(),
            ["workoff"] = Array.Empty<CommandSuggestion>(),
            ["relaxoff"] = Array.Empty<CommandSuggestion>(),
            ["mode"] = new[]
            {
                Choice("mode", "work", "classify new activity as Work"),
                Choice("mode", "relax", "classify new activity as Relax")
            },
            ["assetsweep"] = CleanupChoices("assetsweep"),
            ["sweep"] = CleanupChoices("sweep"),
            ["as"] = CleanupChoices("as"),
            ["hosthealth"] = ToggleWithRun("hosthealth"),
            ["hh"] = ToggleWithRun("hh"),
            ["leavenotices"] = Toggle("leavenotices"),
            ["ln"] = Toggle("ln"),
            ["timestamps"] = new[]
            {
                Choice("timestamps", "on", "enable chat and notice timestamps"),
                Choice("timestamps", "off", "disable chat and notice timestamps"),
                Choice("timestamps", "12", "use 12-hour AM/PM"),
                Choice("timestamps", "24", "use 24-hour time"),
                Choice("timestamps", "reset", "restore enabled 12-hour defaults"),
                Choice("timestamps", "status", "show timestamp state and format")
            },
            ["tt"] = new[]
            {
                Choice("tt", "on", "enable chat and notice timestamps"),
                Choice("tt", "off", "disable chat and notice timestamps"),
                Choice("tt", "12", "use 12-hour AM/PM"),
                Choice("tt", "24", "use 24-hour time"),
                Choice("tt", "reset", "restore enabled 12-hour defaults"),
                Choice("tt", "status", "show timestamp state and format")
            },
            ["chatreadability"] = Toggle("chatreadability"),
            ["chatread"] = Toggle("chatread"),
            ["chatbackdrop"] = Toggle("chatbackdrop"),
            ["chatbg"] = Toggle("chatbg"),
            ["chatoutline"] = new[]
            {
                Choice("chatoutline", "75", "default outline"),
                Choice("chatoutline", "50", "lighter outline"),
                Choice("chatoutline", "100", "crisp outline"),
                Choice("chatoutline", "status", "show current intensity")
            },
            ["outline"] = new[]
            {
                Choice("outline", "75", "default outline"),
                Choice("outline", "50", "lighter outline"),
                Choice("outline", "100", "crisp outline"),
                Choice("outline", "status", "show current intensity")
            },
            ["chatscale"] = new[]
            {
                Choice("chatscale", "100", "vanilla whole chat UI"),
                Choice("chatscale", "125", "larger whole chat UI"),
                Choice("chatscale", "150", "extra large whole chat UI"),
                Choice("chatscale", "reset", "return whole UI to vanilla"),
                Choice("chatscale", "status", "show current whole UI scale")
            },
            ["chatresize"] = new[]
            {
                Choice("chatresize", "100", "vanilla whole chat UI"),
                Choice("chatresize", "125", "larger whole chat UI"),
                Choice("chatresize", "150", "extra large whole chat UI"),
                Choice("chatresize", "reset", "return whole UI to vanilla"),
                Choice("chatresize", "status", "show current whole UI scale")
            },
            ["cr"] = new[]
            {
                Choice("cr", "100", "vanilla whole chat UI"),
                Choice("cr", "125", "larger whole chat UI"),
                Choice("cr", "150", "extra large whole chat UI"),
                Choice("cr", "reset", "return whole UI to vanilla"),
                Choice("cr", "status", "show current whole UI scale")
            },
            ["chatfont"] = ChatFontChoices("chatfont"),
            ["chatfontsize"] = ChatFontChoices("chatfontsize"),
            ["cf"] = ChatFontChoices("cf"),
            ["chatheight"] = ChatHeightChoices("chatheight"),
            ["chatstretch"] = ChatHeightChoices("chatstretch"),
            ["chath"] = ChatHeightChoices("chath"),
            ["chatrows"] = ChatHistoryChoices("chatrows"),
            ["chathistory"] = ChatHistoryChoices("chathistory"),
            ["chatr"] = ChatHistoryChoices("chatr"),
            ["chatvanilla"] = Array.Empty<CommandSuggestion>(),
            ["chatreset"] = Array.Empty<CommandSuggestion>(),
            ["chatdefault"] = Array.Empty<CommandSuggestion>(),
            ["chatoutlinecolor"] = HexColor("chatoutlinecolor", "normal outline"),
            ["outlinecolor"] = HexColor("outlinecolor", "normal outline"),
            ["blackoutlinecolor"] = HexColor("blackoutlinecolor", "black text outline"),
            ["blackchatoutline"] = HexColor("blackchatoutline", "black text outline"),
            ["blackoutline"] = HexColor("blackoutline", "black text outline"),
            ["bettermove"] = Toggle("bettermove"),
            ["bm"] = Toggle("bm"),
            ["reconnectguard"] = Toggle("reconnectguard"),
            ["rg"] = Toggle("rg"),
            ["reconnectmessage"] = Toggle("reconnectmessage"),
            ["rm"] = Toggle("rm"),
            ["focusanywhere"] = Toggle("focusanywhere"),
            ["fa"] = Toggle("fa"),
            ["minimaplabels"] = Toggle("minimaplabels"),
            ["mml"] = Toggle("mml"),
            ["playersaver"] = PlayerRenderSaverChoices("playersaver"),
            ["rendercull"] = PlayerRenderSaverChoices("rendercull"),
            ["playercull"] = PlayerRenderSaverChoices("playercull"),
            ["chalk"] = ChalkboardChoices("chalk"),
            ["chalkboard"] = ChalkboardChoices("chalkboard"),
            ["communitybans"] = CommunityBanChoices("communitybans"),
            ["communityban"] = CommunityBanChoices("communityban"),
            ["bansync"] = CommunityBanChoices("bansync"),
            ["cloneshield"] = Toggle("cloneshield"),
            ["cloneguard"] = Toggle("cloneguard"),
            ["identityshield"] = Toggle("identityshield"),
            ["idsync"] = IdentityResyncChoices("idsync"),
            ["identitysync"] = IdentityResyncChoices("identitysync"),
            ["rostersync"] = IdentityResyncChoices("rostersync"),
            ["auditlog"] = AuditLogChoices("auditlog"),
            ["sessionaudit"] = AuditLogChoices("sessionaudit"),
            ["localaudit"] = AuditLogChoices("localaudit"),
            ["motd"] = new[]
            {
                Choice("motd", "clear", "host: go back to the default welcome message")
            },
            ["rebind"] = RebindChoices("rebind"),
            ["bind"] = RebindChoices("bind"),
            ["kb"] = RebindChoices("kb"),
            ["keys"] = RebindChoices("keys"),
            ["fishaid"] = Toggle("fishaid"),
            ["autofish"] = Toggle("autofish"),
            ["selfmove"] = SelfMoveChoices("selfmove"),
            ["bluemove"] = SelfMoveChoices("bluemove"),
            ["qolmove"] = SelfMoveChoices("qolmove"),
            ["copychat"] = Toggle("copychat"),
            ["ccopy"] = Toggle("ccopy"),
            ["chatlinks"] = Toggle("chatlinks"),
            ["clinks"] = Toggle("clinks"),
            ["spoons"] = new[]
            {
                Choice("spoons", "0", "empty spoon drawer"),
                Choice("spoons", "1", "show [1/5sp]"),
                Choice("spoons", "2", "show [2/5sp]"),
                Choice("spoons", "3", "show [3/5sp]"),
                Choice("spoons", "4", "show [4/5sp]"),
                Choice("spoons", "5", "show [5/5sp]"),
                Choice("spoons", "off", "hide spoon tag"),
                Choice("spoons", "on", "show spoon tag"),
                Choice("spoons", "status", "show spoon state"),
                Choice("spoons", "reset", "restore hidden [5/5sp] default")
            },
            ["setmood"] = new[]
            {
                Choice("setmood", "energy", "show [x/5energy]"),
                Choice("setmood", "spoons", "show [x/5spoons]"),
                Choice("setmood", "default", "restore [x/5sp]"),
                Choice("setmood", "reset", "restore [x/5sp]"),
                Choice("setmood", "status", "show current custom label")
            },
            ["spoonlabel"] = new[]
            {
                Choice("spoonlabel", "energy", "show [x/5energy]"),
                Choice("spoonlabel", "spoons", "show [x/5spoons]"),
                Choice("spoonlabel", "default", "restore [x/5sp]"),
                Choice("spoonlabel", "reset", "restore [x/5sp]"),
                Choice("spoonlabel", "status", "show current custom label")
            },
            ["helper"] = new[]
            {
                Choice("helper", "add", "host: authorize a current lobby player"),
                Choice("helper", "remove", "host: revoke a Helper"),
                Choice("helper", "list", "show the host-scoped Helper list"),
                Choice("helper", "reset", "host: remove every Helper")
            },
            // Backward-compatible aliases use the same single Helper model.
            ["lobbyhelper"] = HelperAlias("lobbyhelper"),
            ["lobbymod"] = HelperAlias("lobbymod"),
            ["modhelper"] = HelperAlias("modhelper"),
            ["moderationhelper"] = HelperAlias("moderationhelper"),
            ["brb"] = Array.Empty<CommandSuggestion>(),
            ["afk"] = Array.Empty<CommandSuggestion>(),
            ["back"] = Array.Empty<CommandSuggestion>(),
            ["pingsound"] = new[]
            {
                Choice("pingsound", "on", "enable ping sound"),
                Choice("pingsound", "off", "disable ping sound"),
                Choice("pingsound", "status", "show sound status"),
                Choice("pingsound", "click", "soft UI click"),
                Choice("pingsound", "change", "UI change chirp"),
                Choice("pingsound", "error", "stronger alert"),
                Choice("pingsound", "task", "task-complete sound"),
                Choice("pingsound", "ticket", "ticket reward sound")
            },
            ["setpingcolor"] = new[]
            {
                Choice("setpingcolor", "FFD700", "classic ping yellow highlight"),
                Choice("setpingcolor", "9B59B6", "purple message highlight"),
                Choice("setpingcolor", "00C7BE", "cyan message highlight"),
                Choice("setpingcolor", "FF6B8A", "pink message highlight")
            },
            ["statuscolor"] = HexColor("statuscolor", "status")
                .Concat(new[] { Choice("statuscolor", "reset", "restore warm gold status color") }).ToArray(),
            ["setname"] = new[]
            {
                Choice("setname", "reset", "restore your current Steam persona name")
            },
            ["bsqol"] = QolChoices("bsqol"),
            ["qol"] = QolChoices("qol"),
            ["style"] = StyleChoices("style"),
            ["sty"] = StyleChoices("sty"),
            ["styleui"] = Array.Empty<CommandSuggestion>(),
            ["sui"] = Array.Empty<CommandSuggestion>()
        };

    private void Update()
    {
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
        if (!string.Equals(currentText, _lastObservedInputText, StringComparison.Ordinal) || currentCaret != _lastObservedCaret)
        {
            _lastObservedInputText = currentText;
            _lastObservedCaret = currentCaret;
            RefreshSuggestions(input);
        }

        if (!Input.GetKeyDown(KeyCode.Tab) || Time.unscaledTime - _lastAutocompleteAt < AutocompleteCooldownSeconds)
        {
            return;
        }

        if (_suggestions.Count > 0 && TryAutocomplete(input, _suggestions[0]))
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
        float width = Mathf.Clamp(Screen.width - 24f, 300f, 560f);
        int visibleSuggestions = Mathf.Min(_suggestions.Count, MaxVisibleSuggestions);
        float listHeight = visibleSuggestions * 40f;
        float height = 58f + listHeight;
        float left = Mathf.Clamp(36f, 8f, Mathf.Max(8f, Screen.width - width - 8f));
        Rect rect = new Rect(left, Mathf.Max(36f, Screen.height - height - 118f), width, height);
        if (_popupBackgroundTexture != null)
        {
            GUI.DrawTexture(rect, _popupBackgroundTexture);
        }

        GUILayout.BeginArea(rect, _popupStyle);
        string title = "BlueSage commands • Tab completes the first match";
        GUILayout.Label(title, _popupHeaderStyle);
        GUILayout.Label(_suggestions.Count > MaxVisibleSuggestions
            ? "Scroll for more. Nothing sends until you press Enter."
            : "Nothing sends until you press Enter.", _popupHintStyle);
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, _suggestions.Count > MaxVisibleSuggestions, GUILayout.Width(width - 24f), GUILayout.Height(listHeight + 4f));
        for (int index = 0; index < _suggestions.Count; index++)
        {
            CommandSuggestion suggestion = _suggestions[index];
            if (GUILayout.Button(new GUIContent(suggestion.Label, suggestion.Hint), _popupButtonStyle, GUILayout.Width(width - 48f), GUILayout.Height(36f)))
            {
                TMP_InputField input = GetChatInput();
                if (input != null)
                {
                    TryAutocomplete(input, suggestion);
                }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void RefreshSuggestions(TMP_InputField input)
    {
        string text = input.text ?? string.Empty;
        if (!IsSlashCommandContext(text, input.caretPosition))
        {
            ClearSuggestions();
            return;
        }

        _suggestions = BuildSuggestions(text, input.caretPosition);
    }

    private static IReadOnlyList<CommandSuggestion> BuildSuggestions(string text, int caretPosition)
    {
        string current = text.Substring(0, Math.Max(0, Math.Min(caretPosition, text.Length)));
        if (TryBuildArgumentSuggestions(current, out IReadOnlyList<CommandSuggestion> argumentSuggestions))
        {
            return argumentSuggestions;
        }

        int maxResults = IsCommandBrowserQuery(current) ? MaxBrowserSuggestions : MaxSuggestions;
        return Plugin.GetCommandSuggestions(current, maxResults)
            .Select(CommandSuggestion.FromRegistryLine)
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion.Completion))
            .ToArray();
    }

    private static bool TryBuildArgumentSuggestions(string current, out IReadOnlyList<CommandSuggestion> suggestions)
    {
        suggestions = Array.Empty<CommandSuggestion>();
        string trimmed = current.TrimStart();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) && !trimmed.StartsWith("./", StringComparison.Ordinal))
        {
            return false;
        }

        string withoutPrefix = trimmed.StartsWith("./", StringComparison.Ordinal) ? trimmed.Substring(2) : trimmed.Substring(1);
        int firstSpace = withoutPrefix.IndexOf(' ');
        if (firstSpace < 0)
        {
            return false;
        }

        string command = withoutPrefix.Substring(0, firstSpace);
        string argumentPrefix = withoutPrefix.Substring(firstSpace + 1).TrimStart();
        int lastSpace = argumentPrefix.LastIndexOf(' ');
        string currentToken = lastSpace >= 0 ? argumentPrefix.Substring(lastSpace + 1) : argumentPrefix;

        // Protected commands remain executable so a manually typed command can
        // fail closed with one calm message, but neither their roots nor their
        // player/incident arguments are discoverable to ordinary lobby users.
        if (Plugin.IsHelperManagementCommandName(command) && !Plugin.CanManageHelpers())
        {
            suggestions = Array.Empty<CommandSuggestion>();
            return true;
        }
        if (Plugin.IsLobbySafetyCommandName(command) && !Plugin.CanUseLobbySafety(out _))
        {
            suggestions = Array.Empty<CommandSuggestion>();
            return true;
        }

        if (string.Equals(command, "sidincident", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "cloneincident", StringComparison.OrdinalIgnoreCase))
        {
            suggestions = BuildIncidentSuggestions(command, argumentPrefix, currentToken, lastSpace);
            return true;
        }

        if (string.Equals(command, "sidban", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "sidunban", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "unbansid", StringComparison.OrdinalIgnoreCase))
        {
            if (lastSpace >= 0)
            {
                suggestions = new[] { Choice(command, "confirm", "confirm the exact previewed target within 30 seconds") }
                    .Where(choice => choice.ArgumentToken.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return suggestions.Count > 0;
            }

            suggestions = Plugin.GetModerationTargetSuggestions(!string.Equals(command, "sidban", StringComparison.OrdinalIgnoreCase))
                .Where(value => value.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                .Take(MaxBrowserSuggestions)
                .Select(value => Choice(command, value, string.Equals(command, "sidban", StringComparison.OrdinalIgnoreCase)
                    ? "Clone Shield suspect; preview before confirming"
                    : "on the game's ban list; preview before confirming"))
                .ToArray();
            return suggestions.Count > 0;
        }

        if (string.Equals(command, "sidwho", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "sidlookup", StringComparison.OrdinalIgnoreCase))
        {
            if (lastSpace >= 0 && argumentPrefix.StartsWith("copy ", StringComparison.OrdinalIgnoreCase))
            {
                suggestions = Plugin.GetIdentityLookupSuggestions()
                    .Where(value => !string.Equals(value, "list", StringComparison.OrdinalIgnoreCase) && value.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxBrowserSuggestions)
                    .Select(value => Choice(command, value, "copy this uniquely verified live identity record locally"))
                    .ToArray();
                return suggestions.Count > 0;
            }

            suggestions = Plugin.GetIdentityLookupSuggestions().Concat(new[] { "copy" })
                .Where(value => value.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                .Take(MaxBrowserSuggestions)
                .Select(value => Choice(command, value, value == "list"
                    ? "open the matched player list in Lobby Safety"
                    : "check this player before any action"))
                .ToArray();
            return suggestions.Count > 0;
        }

        if (!ArgumentSuggestions.TryGetValue(command, out IReadOnlyList<CommandSuggestion> choices))
        {
            return false;
        }

        suggestions = choices
            .Where(choice => choice.ArgumentToken.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
            .Take(MaxBrowserSuggestions)
            .ToArray();
        return suggestions.Count > 0;
    }

    private static IReadOnlyList<CommandSuggestion> BuildIncidentSuggestions(string command, string argumentPrefix, string currentToken, int lastSpace)
    {
        if (lastSpace >= 0)
        {
            string incidentId = argumentPrefix.Substring(0, lastSpace).Trim();
            if (string.Equals(incidentId, "clear-player", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(incidentId, "clear-row", StringComparison.OrdinalIgnoreCase))
            {
                return Plugin.GetIdentityLookupSuggestions()
                    .Where(value => !string.Equals(value, "list", StringComparison.OrdinalIgnoreCase) &&
                                    value.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                    .Take(MaxBrowserSuggestions)
                    .Select(value => Choice(command, value, "clear local incident flags only after this verified row resolves uniquely"))
                    .ToArray();
            }
            if (incidentId.Length > 1 && incidentId.StartsWith("C", StringComparison.OrdinalIgnoreCase) && incidentId.IndexOf(' ') < 0)
            {
                return new[]
                    {
                        Choice(command, "resolve", "host-validated resolution for " + incidentId.ToUpperInvariant()),
                        Choice(command, "dismiss", "clear " + incidentId.ToUpperInvariant() + " on this client only")
                    }
                    .Where(choice => choice.ArgumentToken.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            return Array.Empty<CommandSuggestion>();
        }

        IEnumerable<CommandSuggestion> lifecycle = new[]
        {
            Choice(command, "status", "list open and closed Clone Shield incidents"),
            Choice(command, "dismiss-all", "clear every open incident on this client only"),
            Choice(command, "clear-player", "choose one verified player and clear their local incident flags"),
            Choice(command, "clear-history", "remove closed incident history only"),
            Choice(command, "clear-all", "dismiss open incidents locally and clear closed history")
        };
        IEnumerable<CommandSuggestion> openIncidents = CloneIncidentActionController.GetOpenIncidents()
            .Select(incident => Choice(command, incident.Id, "choose this incident, then resolve or dismiss"));

        return lifecycle.Concat(openIncidents)
            .Where(choice => choice.ArgumentToken.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
            .Take(MaxBrowserSuggestions)
            .ToArray();
    }

    private static bool IsCommandBrowserQuery(string current)
    {
        string trimmed = (current ?? string.Empty).Trim();
        return string.Equals(trimmed, "/", StringComparison.Ordinal) ||
               string.Equals(trimmed, "./", StringComparison.Ordinal);
    }

    private static bool TryAutocomplete(TMP_InputField input, CommandSuggestion suggestion)
    {
        if (input == null || string.IsNullOrWhiteSpace(suggestion.Completion))
        {
            return false;
        }

        string text = input.text ?? string.Empty;
        int caret = Math.Max(0, Math.Min(input.caretPosition, text.Length));
        string before = text.Substring(0, caret);
        string after = text.Substring(caret);
        string replacement = suggestion.IsArgument
            ? ReplaceCurrentArgument(before, suggestion.ArgumentToken)
            : suggestion.Completion + " ";
        input.text = replacement + after;
        input.caretPosition = replacement.Length;
        input.Select();
        input.ActivateInputField();
        return true;
    }

    private static string ReplaceCurrentArgument(string beforeCaret, string argumentToken)
    {
        int lastSpace = beforeCaret.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            return beforeCaret + argumentToken + " ";
        }

        return beforeCaret.Substring(0, lastSpace + 1) + argumentToken + " ";
    }

    private static bool IsSlashCommandContext(string text, int caretPosition)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        int caret = Math.Max(0, Math.Min(caretPosition, text.Length));
        string beforeCaret = text.Substring(0, caret).TrimStart();
        return beforeCaret.StartsWith("/", StringComparison.Ordinal) || beforeCaret.StartsWith("./", StringComparison.Ordinal);
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

    private void ClearSuggestions()
    {
        _suggestions = Array.Empty<CommandSuggestion>();
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
        _popupBackgroundTexture = BlueSageUiTheme.GetSolidTexture(PopupBackground(theme));
        _popupStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 10),
            normal = { background = _popupBackgroundTexture, textColor = theme.PopupBodyText }
        };
        _popupHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            normal = { textColor = theme.PopupHeaderText }
        };
        _popupHintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = theme.PopupHintText }
        };
        _popupButtonStyle = BlueSageUiTheme.CreateNeutralButtonStyle(GUI.skin.button, theme, 13);
        _popupButtonStyle.alignment = TextAnchor.MiddleLeft;
        _popupButtonStyle.wordWrap = true;
        _popupButtonStyle.clipping = TextClipping.Overflow;
        _renderedTextureGeneration = BlueSageUiTheme.RuntimeTextureGeneration;
    }

    private static Color PopupBackground(BlueSageUiThemePalette theme)
    {
        Color panel = theme?.Panel ?? new Color(0.06f, 0.07f, 0.11f, 1f);
        return new Color(panel.r * 0.72f, panel.g * 0.72f, panel.b * 0.72f, 1f);
    }

    private static IReadOnlyList<CommandSuggestion> Toggle(string command)
    {
        return new[]
        {
            Choice(command, "on", "turn this feature on"),
            Choice(command, "off", "turn this feature off"),
            Choice(command, "status", "show current state")
        };
    }

    private static IReadOnlyList<CommandSuggestion> IdentityResyncChoices(
        string command)
    {
        var choices = new List<CommandSuggestion>
        {
            Choice(
                command,
                "status",
                "check player matching"),
            Choice(
                command,
                "refresh",
                "redraw the player list"),
        };

        LobbySafetyAccessState access =
            SteamIdModerationController.GetLocalAccessState(out _, out _);
        if (access == LobbySafetyAccessState.Host)
        {
            choices.Add(Choice(
                command,
                "repair",
                "host: recheck player matches"));
        }
        else if (access == LobbySafetyAccessState.HelperReady)
        {
            choices.Add(Choice(
                command,
                "request",
                "ask the host to recheck"));
        }

        return choices;
    }

    private static IReadOnlyList<CommandSuggestion> AuditLogChoices(string command)
    {
        return new[]
        {
            Choice(command, "on", "start saving your chat and game log copies"),
            Choice(command, "off", "stop saving; existing files stay"),
            Choice(command, "status", "show where your copies are saved"),
            Choice(command, "snapshot", "save the newest 8 MB of the game log"),
            Choice(command, "path", "copy your log folder path")
        };
    }

    private static IReadOnlyList<CommandSuggestion> RebindChoices(string command)
    {
        return new[]
        {
            Choice(command, "qolmenu", "change the QoL menu key"),
            Choice(command, "stylemenu", "change the Style Helper key"),
            Choice(command, "fly", "change the Noclip/Fly key"),
            Choice(command, "sweep", "change the manual cleanup key"),
            Choice(command, "reset", "put every key back to its default")
        };
    }

    private static IReadOnlyList<CommandSuggestion> SelfMoveChoices(string command)
    {
        return new[]
        {
            Choice(command, "status", "show your movement settings"),
            Choice(command, "save", "remember where you're standing"),
            Choice(command, "teleport", "go back to your saved spot"),
            Choice(command, "fly", "turn fly on or off"),
            Choice(command, "speed", "set walk speed (0.25 to 5)"),
            Choice(command, "jump", "set jump height (0.25 to 5)"),
            Choice(command, "gravity", "set gravity (0.1 to 5)"),
            Choice(command, "reset", "put movement back to normal")
        };
    }

    private static IReadOnlyList<CommandSuggestion> CleanupChoices(string command)
    {
        return new[]
        {
            Choice(command, "now", "run one manual cleanup now"),
            Choice(command, "on", "enable BlueSage Automatic Cleanup"),
            Choice(command, "off", "disable BlueSage Automatic Cleanup"),
            Choice(command, "status", "show Automatic Cleanup status")
        };
    }

    private static IReadOnlyList<CommandSuggestion> ToggleWithRun(string command)
    {
        return Toggle(command).Concat(new[] { Choice(command, "run", "run a manual check now") }).ToArray();
    }

    private static IReadOnlyList<CommandSuggestion> HexColor(string command, string label)
    {
        return new[]
        {
            Choice(command, "000000", label + " black"),
            Choice(command, "FFFFFF", label + " white"),
            Choice(command, "FFD45D", label + " warm"),
            Choice(command, "70D6FF", label + " cool"),
            Choice(command, "status", "show current color")
        };
    }

    private static IReadOnlyList<CommandSuggestion> ChatHeightChoices(string command)
    {
        return new[]
        {
            Choice(command, "on", "moderate taller chat window"),
            Choice(command, "off", "vanilla chat window"),
            Choice(command, "toggle", "switch taller window on or off"),
            Choice(command, "100", "vanilla chat window"),
            Choice(command, "140", "taller chat window"),
            Choice(command, "180", "very tall chat window"),
            Choice(command, "reset", "return to vanilla window"),
            Choice(command, "status", "show current window size")
        };
    }

    private static IReadOnlyList<CommandSuggestion> ChatFontChoices(string command)
    {
        return new[]
        {
            Choice(command, "on", "larger message text"),
            Choice(command, "off", "vanilla chat text"),
            Choice(command, "toggle", "switch larger message text on or off"),
            Choice(command, "100", "vanilla chat text"),
            Choice(command, "125", "larger message text"),
            Choice(command, "150", "extra large message text"),
            Choice(command, "reset", "return to vanilla text"),
            Choice(command, "status", "show current font size")
        };
    }

    private static IReadOnlyList<CommandSuggestion> ChatHistoryChoices(string command)
    {
        return new[]
        {
            Choice(command, "50", "lighter busy-lobby scrollback"),
            Choice(command, "100", "lighter busy-lobby scrollback"),
            Choice(command, "150", "balanced default"),
            Choice(command, "250", "maximum scrollback; highest UI cost"),
            Choice(command, "reset", "restore the 150-row default"),
            Choice(command, "status", "show configured retained rows")
        };
    }

    private static IReadOnlyList<CommandSuggestion> PlayerRenderSaverChoices(string command)
    {
        return new[]
        {
            Choice(command, "on", "hide far/overflow remote renderers locally"),
            Choice(command, "off", "restore all remote renderers"),
            Choice(command, "toggle", "switch local render saver on or off"),
            Choice(command, "status", "show visible and hidden counts"),
            Choice(command, "reset", "restore default radius and visible count"),
            Choice(command, "radius", "set hide radius in meters"),
            Choice(command, "max", "set closest visible player count"),
            Choice(command, "45", "default radius hint"),
            Choice(command, "32", "default closest visible hint")
        };
    }

    private static IReadOnlyList<CommandSuggestion> ChalkboardChoices(string command)
    {
        return new[]
        {
            Choice(command, "boards", "show which board is 0, 1, and 2"),
            Choice(command, "view", "look at one board without changing it"),
            Choice(command, "clear", "host or Helper: clear one board (a backup is saved first)"),
            Choice(command, "status", "show whether chalkboards are ready"),
            Choice(command, "list", "list your saved boards and backups"),
            Choice(command, "save", "save a board under a name"),
            Choice(command, "load", "host or Helper: ask to put a saved board back"),
            Choice(command, "delete", "delete one of your saves (a backup is kept)"),
            Choice(command, "refresh", "find boards 0-2 again")
        };
    }

    private static IReadOnlyList<CommandSuggestion> CommunityBanChoices(string command)
    {
        return new[]
        {
            Choice(command, "status", "show the Community Safety List state"),
            Choice(command, "refresh", "check the community ban list now; it only adds names, never kicks or bans"),
            Choice(command, "on", "turn Community Safety List checks on"),
            Choice(command, "off", "turn Community Safety List checks off"),
            Choice(command, "attestation", "show technical list details")
        };
    }

    private static IReadOnlyList<CommandSuggestion> StyleChoices(string command)
    {
        return new[]
        {
            Choice(command, "color", "wrap text with Color A"),
            Choice(command, "bold", "toggle bold styling"),
            Choice(command, "italic", "toggle italic styling"),
            Choice(command, "gradient", "use Color A to B"),
            Choice(command, "gradient3", "use Color A to B to C"),
            Choice(command, "status", "build colored status text: status #HEX text")
        };
    }

    private static IReadOnlyList<CommandSuggestion> QolChoices(string command)
    {
        return new[]
        {
            Choice(command, "status", "show the current feature summary"),
            Choice(command, "hosthealth", "run one host-health check")
        };
    }

    private static IReadOnlyList<CommandSuggestion> HelperAlias(string command)
    {
        return new[]
        {
            Choice(command, "add", "host: authorize a current lobby player"),
            Choice(command, "remove", "host: revoke a Helper"),
            Choice(command, "list", "show the host-scoped Helper list"),
            Choice(command, "reset", "host: remove every Helper")
        };
    }

    private static CommandSuggestion Choice(string command, string token, string hint)
    {
        return new CommandSuggestion("/" + command + " " + token + " - " + hint, "/" + command, token, hint, isArgument: true);
    }

    private readonly struct CommandSuggestion
    {
        public CommandSuggestion(string label, string completion, string argumentToken, string hint, bool isArgument)
        {
            Label = label ?? string.Empty;
            Completion = completion ?? string.Empty;
            ArgumentToken = argumentToken ?? string.Empty;
            Hint = hint ?? string.Empty;
            IsArgument = isArgument;
        }

        public string Label { get; }
        public string Completion { get; }
        public string ArgumentToken { get; }
        public string Hint { get; }
        public bool IsArgument { get; }

        public static CommandSuggestion FromRegistryLine(string line)
        {
            string label = line ?? string.Empty;
            string command = label.Split(new[] { ' ' }, 2)[0];
            string hint = label.Contains(" - ") ? label.Substring(label.IndexOf(" - ", StringComparison.Ordinal) + 3) : "complete command";
            return new CommandSuggestion(label, command, string.Empty, hint, isArgument: false);
        }
    }
}
