using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CloneIncidentLedgerTests
{
    private const string Victim = "76561198000000001";
    private const string Suspect = "76561198000000002";

    [SetUp]
    public void SetUp() => CloneIncidentLedger.Reset("109775242669409858");

    [Test]
    public void Record_assigns_stable_incident_and_targets_the_suspect()
    {
        CloneIncident first = CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);
        CloneIncident repeat = CloneIncidentLedger.Record(Victim, Suspect, "avatar", "Robin", "Echo", 110);

        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.EqualTo("C001"));
            Assert.That(repeat.Id, Is.EqualTo("C001"));
            Assert.That(repeat.MatchType, Does.Contain("name"));
            Assert.That(repeat.MatchType, Does.Contain("avatar"));
            Assert.That(CloneIncidentLedger.TryResolveIncident("c001", out CloneIncident resolved), Is.True);
            Assert.That(resolved.SuspectSteamId, Is.EqualTo(Suspect));
            Assert.That(CloneIncidentLedger.IsProtectedVictim(Victim), Is.True);
        });
    }

    [Test]
    public void Lobby_change_resets_incident_numbers_and_protection()
    {
        CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);
        CloneIncidentLedger.Reset("another-lobby");

        Assert.That(CloneIncidentLedger.IsProtectedVictim(Victim), Is.False);
        Assert.That(CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 200).Id, Is.EqualTo("C001"));
    }

    [Test]
    public void Resolved_incident_leaves_bounded_history_but_clears_protection_and_targeting()
    {
        CloneIncident incident = CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);

        Assert.That(CloneIncidentLedger.MarkResolved(incident.Id, "76561198000000003", 120), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(CloneIncidentLedger.Snapshot, Is.Empty);
            Assert.That(CloneIncidentLedger.HistorySnapshot, Has.Count.EqualTo(1));
            Assert.That(CloneIncidentLedger.HistorySnapshot[0].State, Is.EqualTo(CloneIncidentState.Resolved));
            Assert.That(CloneIncidentLedger.IsProtectedVictim(Victim), Is.False);
            Assert.That(CloneIncidentLedger.TryResolveIncident(incident.Id, out _), Is.False);
        });
    }

    [Test]
    public void Stable_id_matches_for_same_ordered_steam_pair_and_new_transition_after_resolution_gets_new_local_id()
    {
        CloneIncident first = CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);
        CloneIncidentLedger.MarkResolved(first.Id, "76561198000000003", 120);
        CloneIncident second = CloneIncidentLedger.Record(Victim, Suspect, "avatar", "Robin", "Echo", 130);

        Assert.Multiple(() =>
        {
            Assert.That(first.StableId, Does.Match("^I[0-9A-F]{8}$"));
            Assert.That(second.StableId, Is.EqualTo(first.StableId));
            Assert.That(second.Id, Is.EqualTo("C002"));
            Assert.That(CloneIncidentLedger.Snapshot, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Local_dismiss_and_clear_closed_do_not_mutate_other_open_incidents()
    {
        CloneIncident first = CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);
        CloneIncident second = CloneIncidentLedger.Record("76561198000000003", "76561198000000004", "avatar", "A", "B", 110);

        Assert.That(CloneIncidentLedger.DismissLocal(first.Id, 120), Is.True);
        Assert.That(CloneIncidentLedger.ClearClosed(), Is.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(CloneIncidentLedger.Snapshot, Has.Count.EqualTo(1));
            Assert.That(CloneIncidentLedger.Snapshot[0].Id, Is.EqualTo(second.Id));
        });
    }

    [Test]
    public void Bulk_local_dismiss_clears_every_open_role_without_erasing_history()
    {
        CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);
        CloneIncidentLedger.Record("76561198000000003", "76561198000000004", "avatar", "A", "B", 110);

        Assert.That(CloneIncidentLedger.DismissAllOpenLocal(120), Is.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(CloneIncidentLedger.Snapshot, Is.Empty);
            Assert.That(CloneIncidentLedger.HistorySnapshot, Has.Count.EqualTo(2));
            Assert.That(CloneIncidentLedger.HistorySnapshot, Has.All.Matches<CloneIncident>(item => item.State == CloneIncidentState.DismissedLocal));
            Assert.That(CloneIncidentLedger.IsProtectedVictim(Victim), Is.False);
        });
    }

    [Test]
    public void Stale_absent_closure_immediately_removes_targeting_and_protection()
    {
        CloneIncident incident = CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);

        Assert.That(CloneIncidentLedger.MarkStaleAbsent(incident.Id, 120), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(CloneIncidentLedger.Snapshot, Is.Empty);
            Assert.That(CloneIncidentLedger.HistorySnapshot.Single().State, Is.EqualTo(CloneIncidentState.StaleAbsent));
            Assert.That(CloneIncidentLedger.IsProtectedVictim(Victim), Is.False);
        });
    }

    [Test]
    public void Resolver_accepts_incident_full_id_or_unique_suffix_but_never_names()
    {
        CloneIncidentLedger.Record(Victim, Suspect, "name", "Robin", "Echo", 100);
        string[] present = { Victim, Suspect, "76561198000000003" };

        Assert.That(ModerationTargetResolver.TryResolve("C001", present, out ModerationTarget incident, out _), Is.True);
        Assert.That(incident.SteamId, Is.EqualTo(Suspect));
        Assert.That(incident.IncidentId, Is.EqualTo("C001"));
        Assert.That(ModerationTargetResolver.TryResolve(Suspect, present, out ModerationTarget full, out _), Is.True);
        Assert.That(full.SteamId, Is.EqualTo(Suspect));
        Assert.That(ModerationTargetResolver.TryResolve("000002", present, out ModerationTarget suffix, out _), Is.True);
        Assert.That(suffix.SteamId, Is.EqualTo(Suspect));
        Assert.That(ModerationTargetResolver.TryResolve("Echo", present, out _, out string error), Is.False);
        Assert.That(error, Does.Contain("incident ID, full SteamID64, or unique 6+ digit suffix"));
    }

    [Test]
    public void Resolver_rejects_ambiguous_or_short_suffix()
    {
        string[] present = { "76561198000000002", "76561199000000002" };
        Assert.That(ModerationTargetResolver.TryResolve("000002", present, out _, out string ambiguous), Is.False);
        Assert.That(ambiguous, Does.Contain("ambiguous"));
        Assert.That(ModerationTargetResolver.TryResolve("002", present, out _, out string shortError), Is.False);
        Assert.That(shortError, Does.Contain("6+"));
    }
}
