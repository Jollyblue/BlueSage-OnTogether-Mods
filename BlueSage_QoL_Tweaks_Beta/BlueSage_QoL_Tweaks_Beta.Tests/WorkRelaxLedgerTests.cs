using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxLedgerTests
{
    [Test]
    public void Create_copies_the_complete_native_baseline_and_selected_mode()
    {
        var numbers = NativeStatisticsSnapshot.FieldNames
            .Where(name => name != "FocusSessionsPerDay")
            .ToDictionary(name => name, _ => 5d);
        Assert.That(
            NativeStatisticsSnapshot.TryCreate(numbers, new[] { 1, 2, 3, 4, 5, 6, 7 }, out NativeStatisticsSnapshot? snapshot, out _),
            Is.True);

        WorkRelaxLedger ledger = WorkRelaxLedger.Create(
            "p_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            snapshot!,
            WorkRelaxMode.Relax);

        Assert.Multiple(() =>
        {
            Assert.That(ledger.SchemaVersion, Is.EqualTo(1));
            Assert.That(ledger.LastMode, Is.EqualTo("Relax"));
            Assert.That(ledger.Baseline.Numbers, Has.Count.EqualTo(40));
            Assert.That(ledger.Baseline.Numbers.Single(item => item.Key == "TotalFocusDuration").Value, Is.EqualTo(5d));
            Assert.That(ledger.Baseline.FocusSessionsPerDay, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(ledger.LastReconciled.Numbers, Is.Not.SameAs(ledger.Baseline.Numbers));
        });
    }

    [Test]
    public void Codec_round_trips_structural_events_and_corrections_without_private_content()
    {
        const string rawIdentity = "76561198000054321";
        string profileKey = WorkRelaxProfileKey.Create(rawIdentity);
        var ledger = new WorkRelaxLedger
        {
            SchemaVersion = 1,
            ProfileKey = profileKey,
            LastMode = "Work",
            Baseline = new WorkRelaxLedgerStatistics
            {
                Numbers = { new WorkRelaxLedgerMetric { Key = "TotalFocusDuration", Value = 3600d } },
                FocusSessionsPerDay = new[] { 1, 2, 3, 4, 5, 6, 7 }
            },
            LastReconciled = new WorkRelaxLedgerStatistics
            {
                Numbers = { new WorkRelaxLedgerMetric { Key = "TotalFocusDuration", Value = 3660d } },
                FocusSessionsPerDay = new[] { 1, 2, 3, 4, 5, 6, 8 }
            },
            Recovery = new WorkRelaxLedgerRecovery
            {
                LastSuccessfulSaveUtcTicks = 638896322000000000L,
                RecoveryCount = 2
            }
        };

        ledger.AppendEvent(new WorkRelaxLedgerEvent
        {
            EventId = "event-1",
            UtcTicks = 638896320000000000L,
            MetricFamily = "PomodoroDuration",
            MetricKey = "TotalFocusDuration",
            Amount = 60d,
            Mode = "Work",
            Source = "NativePostfix"
        });
        ledger.AppendCorrection(new WorkRelaxLedgerEvent
        {
            EventId = "correction-1",
            UtcTicks = 638896321000000000L,
            MetricFamily = "PomodoroDuration",
            MetricKey = "TotalFocusDuration",
            Amount = 60d,
            Mode = "Relax",
            Source = "UserCorrection",
            TargetEventId = "event-1"
        });

        string json = WorkRelaxLedgerCodec.Serialize(ledger);
        WorkRelaxLedger copy = WorkRelaxLedgerCodec.Deserialize(json);

        Assert.Multiple(() =>
        {
            Assert.That(copy.SchemaVersion, Is.EqualTo(1));
            Assert.That(copy.ProfileKey, Is.EqualTo(profileKey));
            Assert.That(copy.LastMode, Is.EqualTo("Work"));
            Assert.That(copy.Baseline.Numbers.Single().Key, Is.EqualTo("TotalFocusDuration"));
            Assert.That(copy.Baseline.Numbers.Single().Value, Is.EqualTo(3600d));
            Assert.That(copy.LastReconciled.FocusSessionsPerDay, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 8 }));
            Assert.That(copy.Events.Single().EventId, Is.EqualTo("event-1"));
            Assert.That(copy.Events.Single().Amount, Is.EqualTo(60d));
            Assert.That(copy.Corrections.Single().TargetEventId, Is.EqualTo("event-1"));
            Assert.That(
                () => ((IList<WorkRelaxLedgerEvent>)copy.Events).Add(new WorkRelaxLedgerEvent()),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(
                () => ((IList<WorkRelaxLedgerEvent>)copy.Corrections).Clear(),
                Throws.TypeOf<NotSupportedException>());
            Assert.That(copy.Recovery.RecoveryCount, Is.EqualTo(2));
            Assert.That(json, Does.Not.Contain(rawIdentity));
        });

        string[] forbidden = { "task text", "journal", "headline", "contentpath", "chat", "displayname", "steamid64" };
        foreach (string term in forbidden)
            Assert.That(json.ToLowerInvariant(), Does.Not.Contain(term));
    }

    [Test]
    public void Profile_key_is_stable_and_separates_different_local_identities()
    {
        string first = WorkRelaxProfileKey.Create("76561198000054321");
        string repeated = WorkRelaxProfileKey.Create(" 76561198000054321 ");
        string second = WorkRelaxProfileKey.Create("76561198000054322");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(repeated));
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Does.Match("^p_[0-9a-f]{64}$"));
            Assert.That(first, Does.Not.Contain("76561198000054321"));
        });
    }

    [Test]
    public void Append_history_is_isolated_from_caller_and_exposed_record_mutation()
    {
        const string profileKey = "p_eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var ledger = new WorkRelaxLedger { SchemaVersion = 1, ProfileKey = profileKey };
        var originalEvent = new WorkRelaxLedgerEvent
        {
            EventId = "event-stable",
            UtcTicks = 638896320000000000L,
            MetricFamily = "PomodoroDuration",
            MetricKey = "TotalFocusDuration",
            Amount = 60d,
            Mode = "Work",
            Source = "NativePostfix"
        };
        var originalCorrection = new WorkRelaxLedgerEvent
        {
            EventId = "correction-stable",
            UtcTicks = 638896321000000000L,
            MetricFamily = "PomodoroDuration",
            MetricKey = "TotalFocusDuration",
            Amount = 60d,
            Mode = "Relax",
            Source = "UserCorrection",
            TargetEventId = "event-stable"
        };

        ledger.AppendEvent(originalEvent);
        ledger.AppendCorrection(originalCorrection);
        originalEvent.Amount = 999d;
        originalEvent.Mode = "Relax";
        originalCorrection.TargetEventId = "different-event";
        ledger.Events.Single().Amount = 777d;
        ledger.Corrections.Single().Mode = "Work";

        WorkRelaxLedger persisted = WorkRelaxLedgerCodec.Deserialize(WorkRelaxLedgerCodec.Serialize(ledger));

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Events.Single().Amount, Is.EqualTo(60d));
            Assert.That(persisted.Events.Single().Mode, Is.EqualTo("Work"));
            Assert.That(persisted.Corrections.Single().Mode, Is.EqualTo("Relax"));
            Assert.That(persisted.Corrections.Single().TargetEventId, Is.EqualTo("event-stable"));
        });
    }
}
