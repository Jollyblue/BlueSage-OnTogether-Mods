using System.Text.Json;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerLookupPolicyTests
{
    private static readonly PlayerLookupCandidate[] Players =
    {
        new()
        {
            SteamId = "76561198000011111",
            DisplayNameRaw = "<#0E6EED><b>Robin</b></color>",
            DisplayNamePlain = "Robin",
            SteamPersona = "robin_bamboo",
            Role = "PLAYER",
        },
        new()
        {
            SteamId = "76561198000022222",
            DisplayNameRaw = "<rotate=2>Bloo</rotate>",
            DisplayNamePlain = "Bloo",
            SteamPersona = "Blue Bamboo",
            Role = "HELPER",
        },
        new()
        {
            SteamId = "76561198000033333",
            DisplayNameRaw = "Blue",
            DisplayNamePlain = "Blue",
            SteamPersona = "SageBlue",
            Role = "HOST",
            IsLocal = true,
        },
    };

    [TestCase("Rob", "76561198000011111")]
    [TestCase("bamboo", "76561198000011111", "76561198000022222")]
    [TestCase("^Bl.*o$", "76561198000022222")]
    [TestCase("Blue", "76561198000022222", "76561198000033333")]
    [TestCase("02222", "76561198000022222")]
    [TestCase("<rotate=2>", "76561198000022222")]
    [TestCase("_host", "76561198000033333")]
    [TestCase("_self", "76561198000033333")]
    public void Lookup_matches_partial_display_raw_persona_regex_id_and_roles(string query, params string[] expected)
    {
        IReadOnlyList<PlayerLookupCandidate> matches = PlayerLookupPolicy.FindMatches(query, Players, out string error);

        Assert.That(error, Is.Empty);
        Assert.That(matches.Select(item => item.SteamId), Is.EquivalentTo(expected));
    }

    [Test]
    public void Lookup_rejects_short_numeric_queries_and_never_throws_on_invalid_or_expensive_regex()
    {
        Assert.That(PlayerLookupPolicy.FindMatches("222", Players, out string shortError), Is.Empty);
        Assert.That(shortError, Does.Contain("at least 4"));

        Assert.DoesNotThrow(() => PlayerLookupPolicy.FindMatches("[", Players, out _));
        Assert.DoesNotThrow(() => PlayerLookupPolicy.FindMatches("(a+)+$", Players, out _));
    }
}

public sealed class SessionAuditPolicyTests
{
    [Test]
    public void Chat_record_is_valid_json_and_preserves_raw_rich_text_beside_plain_text()
    {
        string json = SessionAuditPolicy.BuildChatRecord(
            "2026-07-14T15:00:00.000Z",
            "global",
            2,
            "verified-current-row",
            "host",
            "HELPER",
            "76561198000022222",
            "<#0E6EED><b>Bloo</b></color>",
            "<rotate=2>Bloo</rotate>",
            "Blue Bamboo",
            "<b>Hello</b> \"lobby\"\nnext",
            "109775241000000000");

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.That(root.GetProperty("displayNameRaw").GetString(), Is.EqualTo("<#0E6EED><b>Bloo</b></color>"));
        Assert.That(root.GetProperty("captureStage").GetString(), Is.EqualTo("rendered-ui"));
        Assert.That(root.GetProperty("observerAuthority").GetString(), Is.EqualTo("host"));
        Assert.That(root.GetProperty("senderRole").GetString(), Is.EqualTo("HELPER"));
        Assert.That(root.GetProperty("displayNamePlain").GetString(), Is.EqualTo("Bloo"));
        Assert.That(root.GetProperty("rosterNamePlain").GetString(), Is.EqualTo("Bloo"));
        Assert.That(root.GetProperty("messageRaw").GetString(), Is.EqualTo("<b>Hello</b> \"lobby\"\nnext"));
        Assert.That(root.GetProperty("messagePlain").GetString(), Is.EqualTo("Hello \"lobby\" next"));
    }

    [Test]
    public void Moderation_record_keeps_exact_identity_fields_for_local_audit()
    {
        string json = SessionAuditPolicy.BuildModerationRecord(
            "2026-07-14T15:00:00.000Z",
            "ban-added",
            "native-ban-store-observed",
            "persisted",
            "76561198000022222",
            "<#0E6EED>Bloo</color>",
            "Blue Bamboo",
            "109775241000000000");

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.That(document.RootElement.GetProperty("steamId64").GetString(), Is.EqualTo("76561198000022222"));
        Assert.That(document.RootElement.GetProperty("displayNamePlain").GetString(), Is.EqualTo("Bloo"));
        Assert.That(document.RootElement.GetProperty("source").GetString(), Is.EqualTo("native-ban-store-observed"));
        Assert.That(document.RootElement.TryGetProperty("issuerSteamId64", out _), Is.True);
        Assert.That(document.RootElement.TryGetProperty("incidentId", out _), Is.True);
        Assert.That(document.RootElement.TryGetProperty("targetRole", out _), Is.True);
    }
}
