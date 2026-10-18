using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Host -> Helper relay of transport-verified SteamID results (2026-10-17). Only the lobby owner can
// write Steam lobby data, so an attestation naming the current owner is host-authored. Helpers
// consume it; ordinary players never do. It carries only verdicts on SIDs the game already shares.
public sealed class SteamIdTruthAttestationPolicyTests
{
    private const string Owner = "76561198000000100";
    private const string Alice = "76561198000000001";
    private const string Bob = "76561198000000002";
    private const long Now = 1_800_000_000L;

    private static string Sample(long issued = Now, string owner = Owner) =>
        SteamIdTruthAttestationPolicy.Build(owner, issued, new[]
        {
            new SteamIdTruthRow("11", Alice, SteamIdTruthState.TransportVerified),
            new SteamIdTruthRow("12", Bob, SteamIdTruthState.TransportMismatch),
            new SteamIdTruthRow("13", "76561198000000003", SteamIdTruthState.LobbyCorroborated),
        });

    [Test]
    public void Build_publishes_only_transport_verdicts()
    {
        Assert.That(Sample(), Is.EqualTo("sidtruth1|" + Owner + "|" + Now + "|11=" + Alice + ":V;12=" + Bob + ":X"));
    }

    [Test]
    public void Fresh_attestation_from_the_current_owner_round_trips()
    {
        bool parsed = SteamIdTruthAttestationPolicy.TryParse(Sample(), Owner, Now + 3, out var entries);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(entries.Count, Is.EqualTo(2));
            Assert.That(entries["11"].ClaimedSteamId, Is.EqualTo(Alice));
            Assert.That(entries["11"].Verified, Is.True);
            Assert.That(entries["12"].Verified, Is.False);
        });
    }

    [TestCase(Now + 21, TestName = "Stale_attestation_is_rejected")]
    [TestCase(Now - 6, TestName = "Future_dated_attestation_beyond_clock_skew_is_rejected")]
    public void Attestation_outside_the_lease_window_is_rejected(long now)
    {
        Assert.That(SteamIdTruthAttestationPolicy.TryParse(Sample(), Owner, now, out _), Is.False);
    }

    [Test]
    public void Attestation_left_behind_by_a_previous_owner_is_rejected()
    {
        Assert.That(SteamIdTruthAttestationPolicy.TryParse(Sample(owner: Alice), Owner, Now, out _), Is.False);
    }

    [TestCase("")]
    [TestCase("sidtruth0|76561198000000100|1800000000|11=76561198000000001:V")]
    [TestCase("sidtruth1|76561198000000100|1800000000|11=76561198000000001:Q")]
    [TestCase("sidtruth1|76561198000000100|1800000000|0=76561198000000001:V")]
    [TestCase("sidtruth1|76561198000000100|1800000000|11=not-a-sid:V")]
    [TestCase("sidtruth1|76561198000000100|1800000000|11=76561198000000001:V;11=76561198000000002:V")]
    public void Malformed_or_duplicate_attestation_is_rejected_whole(string value)
    {
        Assert.That(SteamIdTruthAttestationPolicy.TryParse(value, Owner, Now, out _), Is.False);
    }

    [Test]
    public void Empty_verdict_list_is_a_valid_attestation()
    {
        string value = SteamIdTruthAttestationPolicy.Build(Owner, Now, System.Array.Empty<SteamIdTruthRow>());

        Assert.Multiple(() =>
        {
            Assert.That(SteamIdTruthAttestationPolicy.TryParse(value, Owner, Now, out var entries), Is.True);
            Assert.That(entries, Is.Empty);
        });
    }

    [Test]
    public void Build_stops_before_exceeding_the_Steam_lobby_data_value_limit()
    {
        var rows = new System.Collections.Generic.List<SteamIdTruthRow>();
        for (int i = 1; i <= 400; i++)
        {
            rows.Add(new SteamIdTruthRow(i.ToString(), "7656119800000" + i.ToString("D4"), SteamIdTruthState.TransportVerified));
        }

        string value = SteamIdTruthAttestationPolicy.Build(Owner, Now, rows);

        Assert.Multiple(() =>
        {
            Assert.That(value.Length, Is.LessThanOrEqualTo(SteamIdTruthAttestationPolicy.MaxValueLength));
            Assert.That(SteamIdTruthAttestationPolicy.TryParse(value, Owner, Now, out _), Is.True);
        });
    }

    [Test]
    public void Helper_sees_host_verified_player_even_outside_the_Steam_lobby_list()
    {
        SteamIdTruthAttestationPolicy.TryParse(Sample(), Owner, Now, out var entries);

        SteamIdTruthState state = SteamIdTruthPolicy.ClassifyFromHostAttestation(Alice, "11", entries, isUniqueCurrentLobbyMember: false);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.HostAttestedVerified));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.True);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Helper_sees_host_flagged_spoof_and_lobby_membership_never_overrides_it(bool inLobby)
    {
        SteamIdTruthAttestationPolicy.TryParse(Sample(), Owner, Now, out var entries);

        SteamIdTruthState state = SteamIdTruthPolicy.ClassifyFromHostAttestation(Bob, "12", entries, inLobby);

        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(SteamIdTruthState.HostAttestedMismatch));
            Assert.That(SteamIdTruthPolicy.IsTrusted(state), Is.False);
            Assert.That(SteamIdTruthPolicy.IsSpoofed(state), Is.True);
        });
    }

    [Test]
    public void Helper_row_whose_claim_disagrees_with_the_host_record_is_never_trusted()
    {
        SteamIdTruthAttestationPolicy.TryParse(Sample(), Owner, Now, out var entries);

        Assert.That(
            SteamIdTruthPolicy.ClassifyFromHostAttestation(Bob, "11", entries, isUniqueCurrentLobbyMember: true),
            Is.EqualTo(SteamIdTruthState.Unconfirmed));
    }

    [TestCase(true, "LobbyCorroborated")]
    [TestCase(false, "Unconfirmed")]
    public void Helper_row_the_host_has_not_attested_falls_back_to_lobby_evidence(bool inLobby, string expected)
    {
        SteamIdTruthAttestationPolicy.TryParse(Sample(), Owner, Now, out var entries);

        Assert.That(
            SteamIdTruthPolicy.ClassifyFromHostAttestation("76561198000000009", "99", entries, inLobby),
            Is.EqualTo(System.Enum.Parse<SteamIdTruthState>(expected)));
    }
}
