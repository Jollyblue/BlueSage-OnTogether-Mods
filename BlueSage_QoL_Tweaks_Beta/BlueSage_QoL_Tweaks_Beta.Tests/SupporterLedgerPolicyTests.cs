using System;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using BlueSage.QoLTweaks.Data;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class SupporterLedgerPolicyTests
{
    [Test]
    public void Generated_ledger_contains_the_exact_authorized_projection()
    {
        string[] actual = SupporterLedgerData.Transactions
            .Select(transaction => string.Join("\u001f", new[]
            {
                transaction.DateTimeUtc,
                transaction.From,
                transaction.Message,
                transaction.Received
            }))
            .ToArray();

        string[] expected =
        {
            Row("2026-07-06 21:12 UTC", "Marbles", "Thank you for all of your constant hard work in creating mods for On Together and maintaining such a positive, safe, and  supportive server for everyone. ", "20.00"),
            Row("2026-07-15 00:58 UTC", "Nell", "Share with Rowan!!!", "20.00"),
            Row("2026-07-15 20:58 UTC", "Anon", "Thank you for your mods!! Wishing you the best! :)", "10.00"),
            Row("2026-07-17 22:55 UTC", "Dawn", "Thanks for hosting a great community, made many friends thanks to you", "5.00"),
            Row("2026-07-17 23:06 UTC", "Forty", "Thanks for giving me a lil community to work with <3", "2.00"),
            Row("2026-07-17 23:26 UTC", "keke", "thank you for all you've done in the OT community!! we appreciate you!!", "20.00"),
            Row("2026-07-17 23:29 UTC", "Brettknee", "Thank you for this little world. I'm new to it, but I already feel at home <3", "2.00"),
            Row("2026-07-17 23:29 UTC", "smorescubed", "", "50.00"),
            Row("2026-07-17 23:34 UTC", "smorescubed", "", "50.00"),
            Row("2026-07-17 23:50 UTC", "smorescubed", "", "20.00"),
            Row("2026-07-18 00:04 UTC", "Lexiaphantasia", "You do amazing work! <3 Appreciate it.", "50.00"),
            Row("2026-07-18 00:10 UTC", "ShyGuy", "For the price of an average coffee from a certain barnes and noble-affiliated cafe, it's worth contributing even a little to someone who is doing something so big for the community.", "10.00"),
            Row("2026-07-18 00:49 UTC", "Marbles", "It's not about the mods. You've created a community. ", "50.00"),
            Row("2026-07-18 01:16 UTC", "smorescubed", "<333", "2.00"),
            Row("2026-07-18 01:25 UTC", "Akayra", "Look at you, building communities and stuff! Hang in there bud!", "100.00"),
            Row("2026-07-18 04:08 UTC", "Ashcan", "Thank you for the community you’ve built! I’m always excited to get online to see my “coworkers.” ", "20.00"),
            Row("2026-07-18 23:29 UTC", "Lily1chan", "hey blue!! its lily :D not sure if u remember me! but a little birdie told me you've been struggling the past few weeks. I dont have much... but I hope this will help a bit! thank you so much for being super kind, your community has been one of the lovelist ive been a part of! ", "2.00"),
            Row("2026-07-25 15:31 UTC", "Neonicalle", "Thanks for all your help in the community! Mods aside (though they are great), you've made a corner of the internet a cosier place!", "15.00"),
            Row("2026-07-25 19:32 UTC", "Bochi", "You're a great person blue, and I love the community you've built! Wish I could hang out in game but its lags, though I love hanging out and chatting in the discord! Keep up the good work, and thanks for being an awesome friend! ♥", "50.00"),
            Row("2026-09-16", "Eirian", "Thank you for the OT mod and lobby, and happy birthday! Please consider ear-marking a little for a birthday present; I added extra for that, and we all need a little joy sometimes.", "70.00"),
            Row("2026-10-16", "Lexiaphantasia", "Get Rowan-n-company extra birthday treats :3", "30.00")
        };

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(typeof(SupporterTransaction).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "DateTimeUtc", "From", "Message", "Received" }));
    }

    [Test]
    public void Rollup_matches_the_approved_transaction_identity_message_and_total_counts()
    {
        SupporterIdentityRollup[] rollups = SupporterLedgerPolicy.RollUp(SupporterLedgerData.Transactions);

        Assert.Multiple(() =>
        {
            // 2026-10-17 (Blue): Eirian is new; Lexiaphantasia gave a second time.
            Assert.That(SupporterLedgerData.Transactions.Length, Is.EqualTo(21));
            Assert.That(rollups.Length, Is.EqualTo(16));
            Assert.That(SupporterLedgerPolicy.CountNonEmptyMessages(SupporterLedgerData.Transactions), Is.EqualTo(18));
            Assert.That(rollups.Single(rollup => rollup.Name == "Eirian").TotalReceived, Is.EqualTo(70m));
            Assert.That(rollups.Single(rollup => rollup.Name == "Lexiaphantasia").TotalReceived, Is.EqualTo(80m));
            Assert.That(SupporterLedgerData.Transactions.Length - SupporterLedgerPolicy.CountNonEmptyMessages(SupporterLedgerData.Transactions), Is.EqualTo(3));
            Assert.That(rollups.Single(rollup => rollup.Name == "Marbles").TotalReceived, Is.EqualTo(70m));
            Assert.That(rollups.Single(rollup => rollup.Name == "smorescubed").TotalReceived, Is.EqualTo(122m));
            Assert.That(rollups.Single(rollup => rollup.Name == "Neonicalle").TotalReceived, Is.EqualTo(15m));
            Assert.That(rollups.Single(rollup => rollup.Name == "Bochi").TotalReceived, Is.EqualTo(50m));
            Assert.That(rollups.Any(rollup => rollup.Name == "Lexiaphantasia"), Is.True);
            Assert.That(rollups.Any(rollup => rollup.Name == "Lycoris"), Is.False);
        });
    }

    [Test]
    public void Rollup_is_case_insensitive_and_preserves_the_first_seen_spelling()
    {
        SupporterTransaction[] transactions =
        {
            new("2026-07-01 00:00 UTC", "FirstSpelling", "", "1.00"),
            new("2026-07-02 00:00 UTC", "firstspelling", "hello", "2.00")
        };

        SupporterIdentityRollup rollup = SupporterLedgerPolicy.RollUp(transactions).Single();

        Assert.Multiple(() =>
        {
            Assert.That(rollup.Name, Is.EqualTo("FirstSpelling"));
            Assert.That(rollup.TotalReceived, Is.EqualTo(3m));
            Assert.That(rollup.Transactions.Length, Is.EqualTo(2));
            Assert.That(rollup.MessageCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Public_message_projection_omits_gifts_without_messages_but_preserves_message_order()
    {
        SupporterIdentityRollup supporter = SupporterLedgerPolicy.RollUp(SupporterLedgerData.Transactions)
            .Single(rollup => rollup.Name == "smorescubed");

        SupporterTransaction[] messages = SupporterLedgerPolicy.GetPublicMessages(supporter);

        Assert.Multiple(() =>
        {
            Assert.That(messages, Has.Length.EqualTo(1));
            Assert.That(messages[0].DateTimeUtc, Is.EqualTo("2026-07-18 01:16 UTC"));
            Assert.That(messages[0].Message, Is.EqualTo("<333"));
            Assert.That(messages.All(transaction => !string.IsNullOrWhiteSpace(transaction.Message)), Is.True);
        });
    }

    [Test]
    public void Transaction_heading_has_one_heart_and_only_the_approved_detail_fields()
    {
        string heading = SupporterLedgerPolicy.BuildTransactionHeading(SupporterLedgerData.Transactions[0]);

        Assert.Multiple(() =>
        {
            Assert.That(heading, Is.EqualTo("♥ 2026-07-06 21:12 UTC • Marbles • $20.00"));
            Assert.That(heading.Count(character => character == '♥'), Is.EqualTo(1));
            Assert.That(
                SupporterLedgerData.Transactions.All(transaction =>
                    SupporterLedgerPolicy.BuildTransactionHeading(transaction)
                        .Count(character => character == '♥') == 1),
                Is.True);
        });
    }

    [Test]
    public void Empty_dates_are_labeled_honestly_without_inventing_a_timestamp()
    {
        SupporterTransaction transaction = new("", "Future Friend", "Thank you", "1.00");

        Assert.Multiple(() =>
        {
            Assert.That(SupporterLedgerPolicy.FormatDateLabel(transaction.DateTimeUtc), Is.EqualTo("Date not recorded"));
            Assert.That(SupporterLedgerPolicy.BuildTransactionHeading(transaction), Is.EqualTo("♥ Date not recorded • Future Friend • $1.00"));
        });
    }

    [Test]
    public void Display_sanitizer_neutralizes_rich_text_and_controls_without_eating_heart_emoticons()
    {
        string safe = SupporterLedgerPolicy.SanitizeForDisplay("<b>Hello</b> <3\u0001");

        Assert.Multiple(() =>
        {
            Assert.That(safe.IndexOf("<b>", StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(safe.IndexOf("</b>", StringComparison.Ordinal), Is.EqualTo(-1));
            Assert.That(safe, Does.Contain("<3"));
            Assert.That(safe.IndexOf('\u0001'), Is.EqualTo(-1));
            Assert.That(SupporterLedgerPolicy.GetDisplayMessage(""), Is.EqualTo("No public message was included."));
        });
    }

    private static string Row(string dateTimeUtc, string from, string message, string received) =>
        string.Join("\u001f", new[] { dateTimeUtc, from, message, received });
}
