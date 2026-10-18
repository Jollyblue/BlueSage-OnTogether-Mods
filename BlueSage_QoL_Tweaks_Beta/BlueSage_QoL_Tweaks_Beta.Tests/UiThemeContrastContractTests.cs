using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class UiThemeContrastContractTests
{
    private const double TextMinimum = 4.5;
    private const double BoundaryMinimum = 3.0;
    private static readonly Rgb White = new(1, 1, 1);
    private static readonly Rgb Black = new(0, 0, 0);
    private static readonly Rgb GreenTarget = new(0.45, 0.80, 0.36);
    private static readonly Rgb Experimental = new(1, 0.42, 0.08);

    [Test]
    public void Every_shipped_theme_meets_text_and_component_contrast_contracts()
    {
        foreach (Theme theme in Themes())
        {
            bool midnight = theme.Name == "Midnight Dock";
            bool harbor = theme.Name == "BlueSage Harbor";
            Rgb darkState = midnight ? theme.Window : harbor ? theme.Header : theme.FieldText;
            Rgb section = harbor ? new(0.975, 0.945, 0.885) : Mix(theme.Panel, theme.Window, 0.16);
            Rgb sectionBorder = harbor
                ? new(0.65, 0.52, 0.39)
                : theme.Name == "Vanilla Cream OT"
                    ? Mix(theme.Header, theme.FieldText, 0.40)
                    : Mix(theme.Header, theme.LabelText, 0.30);
            Rgb field = midnight ? new(0.045, 0.065, 0.105) : Mix(section, White, 0.44);
            Rgb fieldHover = midnight ? new(0.075, 0.115, 0.165) : Mix(field, theme.Header, 0.10);
            Rgb fieldFocus = midnight ? new(0.055, 0.135, 0.165) : Mix(field, White, 0.16);
            Rgb fieldBorder = midnight ? new(0.28, 0.42, 0.56) : sectionBorder;
            Rgb active = harbor ? new(0.91, 0.49, 0.39) : theme.Header;
            Rgb focusBorder = midnight ? new(0.24, 0.72, 0.78) : Mix(active, darkState, 0.18);
            Rgb activeText = midnight ? theme.ActiveText : darkState;
            Rgb enabled = harbor ? new(0.39, 0.55, 0.24) : Mix(theme.Header, GreenTarget, 0.58);
            Rgb disabled = harbor ? new(0.68, 0.65, 0.61) : midnight ? new(0.26, 0.31, 0.39) : Mix(theme.ButtonText, theme.Window, 0.58);
            Rgb disabledText = midnight ? new(0.84, 0.89, 0.94) : darkState;
            Rgb neutral = midnight ? Mix(theme.Panel, White, 0.12) : Mix(theme.Panel, theme.ButtonText, 0.30);
            Rgb neutralText = midnight ? theme.ButtonText : darkState;
            Rgb footer = harbor ? new(0.055, 0.075, 0.115) : Mix(theme.Header, theme.Window, 0.30);
            Rgb footerText = harbor ? new(0.84, 0.93, 0.98) : theme.HeaderText;
            Rgb previewText = theme.Name is "OG" or "Vanilla Cream OT" ? White : theme.LabelText;
            Rgb popup = Scale(theme.Panel, 0.72);
            Rgb popupBody = midnight ? theme.LabelText : darkState;
            Rgb popupHeader = midnight ? theme.ActiveText : darkState;
            Rgb popupHint = midnight ? Mix(theme.SmallText, theme.LabelText, 0.35) : darkState;
            Rgb navigationKeyword = midnight ? new(112d / 255d, 214d / 255d, 1d) : new(0d, 90d / 255d, 133d / 255d);

            Assert.Multiple(() =>
            {
                PassText(theme, "window label", theme.LabelText, theme.Window);
                PassText(theme, "panel label", theme.LabelText, theme.Panel);
                PassText(theme, "section label", theme.LabelText, section);
                PassText(theme, "header", theme.HeaderText, theme.Header);
                PassText(theme, "field normal", theme.FieldText, field);
                PassText(theme, "field hover", theme.FieldText, fieldHover);
                PassText(theme, "field focus", theme.FieldText, fieldFocus);
                PassText(theme, "footer", footerText, footer);
                PassText(theme, "preview fallback", previewText, theme.Preview);
                PassText(theme, "popup body", popupBody, popup);
                PassText(theme, "popup header", popupHeader, popup);
                PassText(theme, "popup hint", popupHint, popup);
                PassText(theme, "navigation keyword", navigationKeyword, theme.Panel);

                PassButton(theme, "active", activeText, active);
                PassButton(theme, "enabled", darkState, enabled);
                PassButton(theme, "disabled", disabledText, disabled);
                PassButton(theme, "experimental", darkState, Experimental);
                PassButton(theme, "neutral", neutralText, neutral);

                PassBoundary(theme, "section", sectionBorder, section);
                PassBoundary(theme, "field", fieldBorder, field);
                PassBoundary(theme, "focused field", focusBorder, fieldFocus);
                PassBoundary(theme, "experimental section", midnight ? Experimental : Mix(Experimental, darkState, 0.35), section);
                PassBoundary(theme, "footer", midnight ? focusBorder : harbor ? sectionBorder : darkState, footer);
            });
        }
    }

    [Test]
    public void Source_uses_owned_backgrounds_dynamic_swatch_text_and_contrast_aware_states()
    {
        string root = FindProjectRoot();
        string theme = File.ReadAllText(Path.Combine(root, "src", "BlueSageUiTheme.cs"));
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src", "StyleHelperWindow.cs"));
        Assert.Multiple(() =>
        {
            Assert.That(theme, Does.Contain("bool lightText = RelativeLuminance(text) >= 0.50f"));
            Assert.That(theme, Does.Contain("CreateNeutralButtonStyle"));
            Assert.That(theme, Does.Contain("BestTextColor"));
            Assert.That(theme, Does.Contain("ExperimentalBorder"));
            Assert.That(menu, Does.Contain("_headerBoxStyle"));
            Assert.That(menu, Does.Not.Contain("GUI.contentColor = GetFeatureSectionAccent"));
            Assert.That(helper, Does.Contain("_swatchButtonStyle"));
            Assert.That(helper, Does.Contain("GUI.contentColor = BlueSageUiTheme.BestTextColor"));
        });
    }

    private static void PassButton(Theme theme, string name, Rgb text, Rgb fill)
    {
        bool lightText = Luminance(text) >= 0.50;
        Rgb hover = Mix(fill, lightText ? Black : White, lightText ? 0.08 : 0.11);
        Rgb pressed = Mix(fill, lightText ? Black : White, lightText ? 0.18 : 0.20);
        PassText(theme, name + " normal", text, fill);
        PassText(theme, name + " hover", text, hover);
        PassText(theme, name + " pressed", text, pressed);
    }

    private static void PassText(Theme theme, string name, Rgb foreground, Rgb background)
    {
        Assert.That(Contrast(foreground, background), Is.GreaterThanOrEqualTo(TextMinimum), theme.Name + " " + name);
    }

    private static void PassBoundary(Theme theme, string name, Rgb foreground, Rgb background)
    {
        Assert.That(Contrast(foreground, background), Is.GreaterThanOrEqualTo(BoundaryMinimum), theme.Name + " " + name);
    }

    private static double Contrast(Rgb first, Rgb second)
    {
        double lighter = Math.Max(Luminance(first), Luminance(second));
        double darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Rgb color) =>
        0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(double value) => value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    private static Rgb Mix(Rgb first, Rgb second, double amount) => new(first.R + (second.R - first.R) * amount, first.G + (second.G - first.G) * amount, first.B + (second.B - first.B) * amount);
    private static Rgb Scale(Rgb color, double amount) => new(color.R * amount, color.G * amount, color.B * amount);

    private static IEnumerable<Theme> Themes()
    {
        yield return new("BlueSage Harbor", new(0.93, 0.875, 0.79), new(0.985, 0.95, 0.88), new(0.055, 0.075, 0.115), new(0.84, 0.95, 0.98), new(0.20, 0.16, 0.15), new(0.35, 0.29, 0.27), new(0.22, 0.18, 0.17), new(1, 0.96, 0.88), new(0.12, 0.10, 0.10), new(0.985, 0.95, 0.88));
        yield return new("OG", new(0.93, 0.90, 0.83), new(0.84, 0.80, 0.72), new(0.55, 0.52, 0.47), new(0.12, 0.09, 0.10), new(0.22, 0.14, 0.17), new(0.34, 0.26, 0.29), new(0.24, 0.17, 0.20), White, new(0.14, 0.11, 0.12), new(0.28, 0.27, 0.25));
        yield return new("Vanilla Cream OT", new(0.98, 0.93, 0.84), new(0.91, 0.82, 0.69), new(0.91, 0.49, 0.42), new(0.23, 0.14, 0.16), new(0.32, 0.22, 0.25), new(0.43, 0.31, 0.33), new(0.31, 0.22, 0.25), White, new(0.15, 0.11, 0.12), new(0.39, 0.31, 0.27));
        yield return new("Midnight Dock", new(0.08, 0.10, 0.16), new(0.13, 0.16, 0.24), new(0.08, 0.36, 0.42), new(0.88, 0.98, 1), new(0.91, 0.96, 1), new(0.72, 0.82, 0.90), new(0.89, 0.94, 1), new(1, 0.91, 0.42), new(0.96, 0.98, 1), new(0.04, 0.05, 0.08));
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "src", "BlueSageUiTheme.cs"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }

    private readonly record struct Rgb(double R, double G, double B);
    private sealed record Theme(string Name, Rgb Window, Rgb Panel, Rgb Header, Rgb HeaderText, Rgb LabelText, Rgb SmallText, Rgb ButtonText, Rgb ActiveText, Rgb FieldText, Rgb Preview);
}
