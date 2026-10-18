using BlueSage.QoLTweaks.Core;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HeaderActionLayoutPolicyTests
{
    [TestCase(1380f, 1.5f)]
    [TestCase(840f, 1.5f)]
    [TestCase(760f, 2f)]
    [TestCase(640f, 2f)]
    public void Header_actions_fit_representative_physical_widths_and_display_scales(float physicalWidth, float scale)
    {
        float logicalWidth = physicalWidth / scale;
        HeaderActionLayout layout = HeaderActionLayoutPolicy.Resolve(logicalWidth);
        HeaderActionRect[] actions = { layout.Website, layout.Thunderstore, layout.Kofi, layout.Discord, layout.Close };
        string[] labels = { layout.WebsiteLabel, layout.ThunderstoreLabel, layout.KofiLabel, layout.DiscordLabel, layout.CloseLabel };

        Assert.Multiple(() =>
        {
            Assert.That(layout.Title.X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.Title.Right, Is.LessThanOrEqualTo(layout.Website.X));
            Assert.That(layout.Drag.Right, Is.LessThanOrEqualTo(layout.Website.X));
            Assert.That(actions[0].X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(actions[^1].Right, Is.LessThanOrEqualTo(logicalWidth));
            for (int index = 1; index < actions.Length; index++)
            {
                Assert.That(actions[index - 1].Right, Is.LessThanOrEqualTo(actions[index].X), $"Header actions {index - 1} and {index} overlapped.");
            }
            for (int index = 0; index < actions.Length; index++)
            {
                Assert.That(actions[index].Width, Is.GreaterThanOrEqualTo(MinimumLabelWidth(labels[index])), $"Header label '{labels[index]}' would clip.");
            }
        });
    }

    [Test]
    public void Header_policy_keeps_public_link_geometry_and_labels_deterministic()
    {
        HeaderActionLayout layout = HeaderActionLayoutPolicy.Resolve(920f);

        Assert.Multiple(() =>
        {
            Assert.That(HeaderActionLayoutPolicy.WebsiteUrl, Is.EqualTo("https://bluesagetech.net/"));
            Assert.That(layout.Website.Width, Is.EqualTo(76f));
            Assert.That(layout.Thunderstore.Width, Is.EqualTo(108f));
            Assert.That(layout.Kofi.Width, Is.EqualTo(58f));
            Assert.That(layout.Discord.Width, Is.EqualTo(76f));
            Assert.That(layout.Close.Width, Is.EqualTo(30f));
            Assert.That(layout.Website.X, Is.EqualTo(528f));
            Assert.That(layout.Close.Right, Is.EqualTo(908f));
            Assert.That(layout.WebsiteLabel, Is.EqualTo("Website"));
            Assert.That(layout.ThunderstoreLabel, Is.EqualTo("Thunderstore"));
            Assert.That(layout.KofiLabel, Is.EqualTo("Ko-fi"));
            Assert.That(layout.DiscordLabel, Is.EqualTo("Discord"));
            Assert.That(layout.CloseLabel, Is.EqualTo("X"));
        });
    }

    [Test]
    public void Both_public_windows_use_the_shared_website_and_header_geometry_contract()
    {
        string root = FindProjectRoot();
        string menu = File.ReadAllText(Path.Combine(root, "src", "QolMenuWindow.cs"));
        string helper = File.ReadAllText(Path.Combine(root, "src", "StyleHelperWindow.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(menu, Does.Contain("HeaderActionLayoutPolicy.Resolve(_windowRect.width)"));
            Assert.That(helper, Does.Contain("HeaderActionLayoutPolicy.Resolve(_windowRect.width)"));
            // Blue, 2026-10-18: the website is Coming Soon. The button stays and says so instead of opening a page
            // that isn't live yet.
            foreach (string window in new[] { menu, helper })
            {
                Assert.That(window, Does.Contain("GUIContent(layout.WebsiteLabel, HeaderActionLayoutPolicy.WebsiteTooltip)"));
                Assert.That(window, Does.Contain("ShowToast(HeaderActionLayoutPolicy.WebsiteComingSoonNotice)"));
                Assert.That(window, Does.Not.Contain("Application.OpenURL(HeaderActionLayoutPolicy.WebsiteUrl)"));
            }
            Assert.That(HeaderActionLayoutPolicy.WebsiteTooltip, Does.Contain("Coming Soon"));
            Assert.That(HeaderActionLayoutPolicy.WebsiteComingSoonNotice, Does.Contain("Coming Soon").And.Contain("Discord"));
            Assert.That(menu, Does.Contain("GUIContent(layout.ThunderstoreLabel").And.Contain("GUIContent(layout.KofiLabel").And.Contain("GUIContent(layout.DiscordLabel").And.Contain("GUIContent(layout.CloseLabel"));
            Assert.That(helper, Does.Contain("GUIContent(layout.ThunderstoreLabel").And.Contain("GUIContent(layout.KofiLabel").And.Contain("GUIContent(layout.DiscordLabel").And.Contain("GUIContent(layout.CloseLabel"));
            Assert.That(menu, Does.Contain("GUI.DragWindow(ToUnityRect(headerLayout.Drag));"));
            Assert.That(helper, Does.Contain("GUI.DragWindow(ToUnityRect(headerLayout.Drag));"));
            Assert.That(menu, Does.Not.Contain("_windowRect.width - 138f"));
            Assert.That(helper, Does.Not.Contain("_windowRect.width - 138f"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }

    private static float MinimumLabelWidth(string label) => label switch
    {
        "Website" => 68f,
        "Web" => 44f,
        "Thunderstore" => 100f,
        "Mods" => 48f,
        "Ko-fi" => 46f,
        "Discord" => 68f,
        "Chat" => 46f,
        "X" => 30f,
        _ => float.MaxValue
    };
}
