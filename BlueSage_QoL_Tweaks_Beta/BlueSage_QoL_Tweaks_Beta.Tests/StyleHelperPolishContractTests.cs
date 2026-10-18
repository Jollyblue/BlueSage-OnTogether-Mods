using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StyleHelperPolishContractTests
{
    [Test]
    public void EditorTabsDoNotStretchTheirContentIntoBlankVerticalSpace()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));
        Assert.That(source, Does.Not.Contain("GUILayout.FlexibleSpace();"));
        Assert.That(source, Does.Contain("GUILayout.BeginVertical(_boxStyle, GUILayout.ExpandHeight(false));"));
        Assert.That(source, Does.Contain("stretchHeight = false"));
        Assert.That(source, Does.Contain("richText = false, fontSize = 15"));
        Assert.That(source, Does.Contain("including nested <b>, <rotate>, and <size>"));
        Assert.That(source, Does.Contain("Detected rich-text tags stay visible and editable here"));
    }
    private static string ProjectRoot
    {
        get
        {
            string current = Directory.GetCurrentDirectory();
            while (!File.Exists(Path.Combine(current, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                current = Directory.GetParent(current)?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
            }

            return current;
        }
    }

    [Test]
    public void Style_helper_keeps_footer_tooltips_readable_and_detect_current_truthful()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));
        string menuSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));

        Assert.That(source, Does.Contain("FooterHeight = 156f"));
        Assert.That(source, Does.Contain("CompactFooterHeight = 198f"));
        Assert.That(menuSource, Does.Contain("FooterHeight = 120f"));
        Assert.That(source, Does.Contain("BlueSageWindowHoverScope.RegisterWindow(47057"));
        Assert.That(menuSource, Does.Contain("BlueSageWindowHoverScope.RegisterWindow(47058"));
        Assert.That(source, Does.Not.Contain("BlueSageWindowHoverScope.IsWindowHovered(47057)"));
        Assert.That(menuSource, Does.Contain("BlueSageWindowHoverScope.IsWindowHovered(47058)"));
        Assert.That(source, Does.Contain("GetContentScrollHeight()"));
        Assert.That(source, Does.Contain("BlueSageHelpFrame.BeginFooter"));
        Assert.That(source, Does.Contain("BlueSageHelpFrame.DrawStateAndHelp"));
        Assert.That(source, Does.Contain("_footerTextStyle"));
        Assert.That(source, Does.Contain("BuildActiveTabFooterDescription("));
        Assert.That(source, Does.Contain("ResetTransientEditorStateForLiveSync(resetTab: true);"));
        Assert.That(source, Does.Contain("ResetTransientEditorStateForLiveSync(resetTab: false, clearHistory: false);"));
        Assert.That(source, Does.Contain("HydrateCurrentName(force: true, detected);"));
        Assert.That(source, Does.Contain("_detectedNamePreview"));
        Assert.That(source, Does.Contain("GetCurrentPlayerStyledNameForStyleUi"));
        Assert.That(source, Does.Contain("UseDetectedNamePreviewIfAvailable()"));
        Assert.That(source, Does.Contain("ClearDetectedNamePreview();"));
        Assert.That(source, Does.Contain("Detected current live name style."));
        Assert.That(source, Does.Contain("HydrateCurrentStatus(detected);"));
        Assert.That(source, Does.Contain("HydrateSpoons(detected);"));
        Assert.That(source, Does.Contain("_text = PlaceholderName;"));
        Assert.That(source, Does.Contain("_undoHistory.Clear();"));
        Assert.That(source, Does.Contain("_contentScroll = Vector2.zero;"));
    }

    [Test]
    public void Overlapping_windows_clear_inherited_tooltips_before_drawing_their_own_controls()
    {
        string helper = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));
        string menu = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string helperDraw = Slice(helper, "private void DrawWindow(int id)", "private void DrawHeader()");
        string menuDraw = Slice(menu, "private void DrawWindow(int id)", "private void DrawHeader()");

        Assert.Multiple(() =>
        {
            Assert.That(helperDraw, Does.Contain("GUI.tooltip = string.Empty;"));
            Assert.That(menuDraw, Does.Contain("GUI.tooltip = string.Empty;"));
            Assert.That(helperDraw.IndexOf("GUI.tooltip = string.Empty;", StringComparison.Ordinal), Is.LessThan(helperDraw.IndexOf("DrawHeader();", StringComparison.Ordinal)));
            Assert.That(menuDraw.IndexOf("GUI.tooltip = string.Empty;", StringComparison.Ordinal), Is.LessThan(menuDraw.IndexOf("DrawHeader();", StringComparison.Ordinal)));
        });
    }

    [Test]
    public void Style_helper_footer_description_is_owned_by_the_active_tab_not_the_process_global_tooltip()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("NameStylerDescription"));
            Assert.That(source, Does.Contain("StatusStylerDescription"));
            Assert.That(source, Does.Contain("TemplatesDescription"));
            Assert.That(source, Does.Contain("string inheritedTooltip = GUI.tooltip;"));
            Assert.That(source, Does.Contain("DrawFooter(inheritedTooltip);"));
            Assert.That(source, Does.Contain("BuildActiveTabFooterDescription(inheritedTooltip)"));
            Assert.That(source, Does.Contain("BlueSageHelpFrame.DrawStateAndHelp"));
            Assert.That(source, Does.Contain("private string BuildActiveTabFooterDescription(string inheritedTooltip)"));
            Assert.That(source, Does.Contain("_tab == 1 ? StatusStylerDescription"));
            Assert.That(source, Does.Contain("_tab == 2 ? TemplatesDescription"));
            Assert.That(source, Does.Contain("string scopedTooltip = GUI.tooltip;"));
            Assert.That(source, Does.Contain("private string _lastOwnedFooterTooltip = string.Empty;"));
            Assert.That(source, Does.Contain("bool inheritedFromThisWindow = string.Equals(inheritedTooltip, _lastOwnedFooterTooltip, StringComparison.Ordinal);"));
            Assert.That(source, Does.Contain("string.Equals(scopedTooltip, inheritedTooltip, StringComparison.Ordinal)"));
            Assert.That(source, Does.Contain("&& !inheritedFromThisWindow"));
            Assert.That(source, Does.Contain("_lastOwnedFooterTooltip = scopedTooltip;"));
            Assert.That(source, Does.Contain("return scopedTooltip;"));
            Assert.That(source, Does.Not.Contain("GUILayout.Label(BuildScopedFooterTooltip(), _footerTextStyle"));
            Assert.That(source, Does.Not.Contain("return GUI.tooltip;"));
            Assert.That(source, Does.Contain("string stateLine = \"State: \" + _stateLabel + \" — \" + _stateDetail;"));
        });
    }

    [Test]
    public void Style_helper_has_spoons_specific_undo_and_gradient_copy_layout()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(source, Does.Contain("Undo Spoons"));
        Assert.That(source, Does.Contain("UndoSpoonsLastAction"));
        Assert.That(source, Does.Contain("Spoons undo restored only the last Spoons state"));
        Assert.That(source, Does.Contain("DrawGeneratedTextArea"));
        Assert.That(source, Does.Contain("_generatedTextStyle = new GUIStyle(_textFieldStyle) { wordWrap = false }"));
        Assert.That(source, Does.Contain("GUILayout.TextArea(_preview ?? string.Empty, _generatedTextStyle"));
        Assert.That(source, Does.Not.Contain("GUI.skin.textArea.wordWrap = false"));
        Assert.That(source, Does.Contain("GUILayout.Height(_mode == 3 ? 116f : 90f)"));
        Assert.That(source, Does.Contain("DrawSpoonCustomLabelRow();"));
        Assert.That(source.IndexOf("DrawSpoonCustomLabelRow();"), Is.GreaterThan(source.IndexOf("private void DrawSpoonsControls()")));
        Assert.That(source, Does.Contain("_contentScroll = Vector2.zero;"));
        string tabsBody = source.Substring(source.IndexOf("private void DrawTabs()"), source.IndexOf("private void DrawTabButton") - source.IndexOf("private void DrawTabs()"));
        Assert.That(tabsBody, Does.Not.Contain("Custom label"));
    }

    [Test]
    public void Name_and_status_stylers_share_the_same_color_library_layout()
    {
        string source = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(source, Does.Contain("DrawPresetColorLibrary("));
        Assert.That(source, Does.Contain("Apply #\" + preset + \" to Color \" + (char)('A' + _activeColorSlot)"));
        Assert.That(source, Does.Contain("\"Use #\" + preset + \" for only your status tag"));
        Assert.That(source, Does.Contain("GUILayout.MinWidth(84f)"));
        Assert.That(source, Does.Not.Contain("for (int index = 0; index < PresetColors.Length; index += 6)"));
    }

    [Test]
    public void Chat_readability_preserves_scrollback_and_menu_tooltips_have_space()
    {
        string readabilitySource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "Patches", "ChatReadabilityPatch.cs"));
        string menuSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));

        Assert.That(readabilitySource, Does.Not.Contain("EnsureChatScrollbackFriendly"));
        Assert.That(readabilitySource, Does.Not.Contain("GetComponentsInChildren<ScrollRect>(true)"));
        Assert.That(readabilitySource, Does.Not.Contain("scrollRect.vertical ="));
        Assert.That(readabilitySource, Does.Not.Contain("scrollRect.movementType"));
        Assert.That(menuSource, Does.Contain("FooterHeight = 120f"));
        Assert.That(menuSource, Does.Contain("BlueSageWindowHoverScope.IsWindowHovered(47058)"));
        Assert.That(menuSource, Does.Contain("GetContentScrollHeight()"));
        Assert.That(menuSource, Does.Contain("BlueSageHelpFrame.BeginFooter"));
        Assert.That(menuSource, Does.Contain("BlueSageHelpFrame.DrawStateAndHelp"));
        Assert.That(menuSource, Does.Contain("_footerTextStyle"));
        Assert.That(menuSource, Does.Contain("BuildScopedFooterTooltip()"));
    }

    [Test]
    public void Helper_windows_auto_fit_to_screen_and_keep_guidance_noticeable()
    {
        string policyPath = Path.Combine(ProjectRoot, "src", "Core", "WindowFitPolicy.cs");
        string coordinatorPath = Path.Combine(ProjectRoot, "src", "BlueSageWindowCoordinator.cs");
        string menuSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "QolMenuWindow.cs"));
        string helperSource = File.ReadAllText(Path.Combine(ProjectRoot, "src", "StyleHelperWindow.cs"));

        Assert.That(File.Exists(policyPath), Is.True);
        Assert.That(File.Exists(coordinatorPath), Is.True);
        string policySource = File.ReadAllText(policyPath);
        string coordinatorSource = File.ReadAllText(coordinatorPath);

        Assert.That(policySource, Does.Contain("DefaultWindowWidth = 1390f"));
        Assert.That(policySource, Does.Contain("DefaultWindowHeight = 1210f"));
        Assert.That(policySource, Does.Contain("MenuDisplayScalePolicy.GetComfortSize(surface)"));
        Assert.That(policySource, Does.Contain("FitInitial"));
        Assert.That(policySource, Does.Contain("FitToScreen"));
        Assert.That(coordinatorSource, Does.Contain("WindowFitPolicy.FitToScreen"));
        Assert.That(coordinatorSource, Does.Contain("WindowFitPolicy.FitForOpen"));
        Assert.That(coordinatorSource, Does.Not.Contain("SharedWindowLayoutPolicy.CreatePair"));
        Assert.That(coordinatorSource, Does.Not.Contain("_qolMenuVisible && _styleHelperVisible"));
        Assert.That(menuSource, Does.Contain("BlueSageWindowCoordinator.ResolveForFrame"));
        Assert.That(helperSource, Does.Contain("BlueSageWindowCoordinator.ResolveForFrame"));
        Assert.That(menuSource, Does.Contain("BlueSageWindowCoordinator.ResolveForOpen"));
        Assert.That(helperSource, Does.Contain("BlueSageWindowCoordinator.ResolveForOpen"));
        Assert.That(menuSource, Does.Not.Contain("expandToMaximum: !_hasManualResize"));
        Assert.That(helperSource, Does.Not.Contain("expandToMaximum: !_hasManualResize"));
        Assert.That(menuSource, Does.Contain("private bool _hasOpened;"));
        Assert.That(helperSource, Does.Contain("private bool _hasOpened;"));
        Assert.That(menuSource, Does.Contain("_hasOpened = true;"));
        Assert.That(helperSource, Does.Contain("_hasOpened = true;"));
        Assert.That(menuSource, Does.Contain("fontSize = 15"));
        Assert.That(helperSource, Does.Contain("fontSize = 15"));
        Assert.That(menuSource, Does.Contain("Tip: hover"));
        Assert.That(helperSource, Does.Contain("Tip:"));
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Start marker missing: " + startMarker);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }
}
