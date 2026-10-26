using System.Collections.Generic;
using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// UAT 2026-10-18: Host Health showed "lobby=?" since round 3. The game's PurrLobby.Lobby is a struct of
// plain fields (Members, MaxPlayers, Name), and the readback only asked for properties, so it never
// found anything. ReflectedMember reads a property or, failing that, a public field.
public sealed class ReflectedMemberTests
{
    private struct LobbyLikeStruct
    {
        public string Name;
        public int MaxPlayers;
        public List<string> Members;
    }

    private sealed class PropertyHolder
    {
        public string Title { get; set; } = "Blue";
    }

    [Test]
    public void Reads_public_fields_of_a_boxed_struct_like_the_games_lobby()
    {
        object lobby = new LobbyLikeStruct { Name = "Blue&Robins", MaxPlayers = 112, Members = new List<string> { "Blue" } };

        Assert.Multiple(() =>
        {
            Assert.That(ReflectedMember.TryRead(lobby, "MaxPlayers", out object max), Is.True);
            Assert.That(max, Is.EqualTo(112));
            Assert.That(ReflectedMember.TryRead(lobby, "Name", out object name), Is.True);
            Assert.That(name, Is.EqualTo("Blue&Robins"));
            Assert.That(ReflectedMember.TryRead(lobby, "Members", out object members), Is.True);
            Assert.That(ReflectedMember.TryRead(members, "Count", out object count), Is.True, "List.Count is a property.");
            Assert.That(count, Is.EqualTo(1));
        });
    }

    [Test]
    public void Reads_properties_and_reports_missing_members_or_targets()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReflectedMember.TryRead(new PropertyHolder(), "Title", out object title), Is.True);
            Assert.That(title, Is.EqualTo("Blue"));
            Assert.That(ReflectedMember.TryRead(new PropertyHolder(), "Nope", out object missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(ReflectedMember.TryRead(null!, "Title", out _), Is.False);
        });
    }

    [Test]
    public void Lobby_readbacks_use_the_field_or_property_reader()
    {
        string plugin = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "Plugin.cs")).Replace("\r\n", "\n");
        string counts = Slice(plugin, "private static bool TryGetVisibleLobbyCounts(out int? memberCount, out int? maxPlayers)", "\n    }\n");

        Assert.Multiple(() =>
        {
            Assert.That(counts, Does.Contain("ReflectedMember.TryRead(currentLobby, \"Members\""));
            Assert.That(counts, Does.Contain("ReflectedMember.TryRead(currentLobby, \"MaxPlayers\""));
            Assert.That(counts, Does.Not.Contain("GetProperty(\"Members\")").And.Not.Contain("GetProperty(\"MaxPlayers\")"));
            // Round 4 R9: TryReadStringProperty/TryReadBoolProperty were part of the removed lobby-surface
            // diagnostics chain; the lobby-count readback above is the one live ReflectedMember consumer in Plugin.cs.
            Assert.That(plugin, Does.Not.Contain("private static bool TryReadStringProperty("));
            Assert.That(plugin, Does.Not.Contain("private static bool TryReadBoolProperty("));
        });
    }

    private static string Slice(string source, string start, string end)
    {
        int s = source.IndexOf(start, System.StringComparison.Ordinal);
        if (s < 0) return string.Empty;
        int e = source.IndexOf(end, s + start.Length, System.StringComparison.Ordinal);
        return e < 0 ? string.Empty : source.Substring(s, e - s);
    }

    private static string ProjectRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
