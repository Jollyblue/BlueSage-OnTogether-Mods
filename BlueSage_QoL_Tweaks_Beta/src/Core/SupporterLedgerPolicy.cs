#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal sealed class SupporterTransaction
{
    public SupporterTransaction(string? dateTimeUtc, string? from, string? message, string? received)
    {
        DateTimeUtc = dateTimeUtc ?? string.Empty;
        From = from ?? string.Empty;
        Message = message ?? string.Empty;
        Received = received ?? string.Empty;
    }

    public string DateTimeUtc { get; }

    public string From { get; }

    public string Message { get; }

    public string Received { get; }
}

internal sealed class SupporterIdentityRollup
{
    public SupporterIdentityRollup(
        string name,
        decimal totalReceived,
        SupporterTransaction[] transactions,
        int messageCount)
    {
        Name = name;
        TotalReceived = totalReceived;
        Transactions = transactions;
        MessageCount = messageCount;
    }

    public string Name { get; }

    public decimal TotalReceived { get; }

    public SupporterTransaction[] Transactions { get; }

    public int MessageCount { get; }
}

internal static class SupporterLedgerPolicy
{
    public const string EmptyMessageLabel = "No public message was included.";

    public static SupporterIdentityRollup[] RollUp(SupporterTransaction[]? transactions)
    {
        if (transactions == null || transactions.Length == 0)
        {
            return new SupporterIdentityRollup[0];
        }

        var ordered = new List<RollupBuilder>();
        var byIdentity = new Dictionary<string, RollupBuilder>(StringComparer.OrdinalIgnoreCase);

        foreach (SupporterTransaction transaction in transactions)
        {
            if (transaction == null)
            {
                continue;
            }

            string identity = transaction.From ?? string.Empty;
            if (!byIdentity.TryGetValue(identity, out RollupBuilder? builder))
            {
                builder = new RollupBuilder(identity);
                byIdentity.Add(identity, builder);
                ordered.Add(builder);
            }

            builder.Add(transaction);
        }

        var result = new SupporterIdentityRollup[ordered.Count];
        for (int index = 0; index < ordered.Count; index++)
        {
            result[index] = ordered[index].Build();
        }

        return result;
    }

    public static int CountNonEmptyMessages(SupporterTransaction[]? transactions)
    {
        if (transactions == null)
        {
            return 0;
        }

        int count = 0;
        foreach (SupporterTransaction transaction in transactions)
        {
            if (transaction != null && !string.IsNullOrEmpty(transaction.Message))
            {
                count++;
            }
        }

        return count;
    }

    public static SupporterTransaction[] GetPublicMessages(SupporterIdentityRollup? supporter)
    {
        if (supporter == null || supporter.Transactions.Length == 0)
        {
            return new SupporterTransaction[0];
        }

        var messages = new List<SupporterTransaction>();
        foreach (SupporterTransaction transaction in supporter.Transactions)
        {
            if (transaction != null && !string.IsNullOrWhiteSpace(transaction.Message))
            {
                messages.Add(transaction);
            }
        }

        return messages.ToArray();
    }

    public static decimal GetTotalReceived(SupporterTransaction[]? transactions)
    {
        if (transactions == null)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (SupporterTransaction transaction in transactions)
        {
            if (transaction != null)
            {
                total += ParseReceived(transaction.Received);
            }
        }

        return total;
    }

    public static string FormatReceived(decimal amount) =>
        "$" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string FormatDateLabel(string? dateTimeUtc) =>
        string.IsNullOrWhiteSpace(dateTimeUtc)
            ? "Date not recorded"
            : SanitizeForDisplay(dateTimeUtc);

    public static string BuildTransactionHeading(SupporterTransaction? transaction)
    {
        if (transaction == null)
        {
            throw new ArgumentNullException(nameof(transaction));
        }

        return "♥ " +
               FormatDateLabel(transaction.DateTimeUtc) +
               " • " +
               SanitizeForDisplay(transaction.From) +
               " • " +
               FormatReceived(ParseReceived(transaction.Received));
    }

    public static string GetDisplayMessage(string? message) =>
        string.IsNullOrEmpty(message) ? EmptyMessageLabel : SanitizeForDisplay(message);

    public static string SanitizeForDisplay(string? value)
    {
        string raw = value ?? string.Empty;
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var safe = new StringBuilder(raw.Length);
        for (int index = 0; index < raw.Length; index++)
        {
            char character = raw[index];
            if (char.IsControl(character))
            {
                continue;
            }

            safe.Append(character);
            if (character == '<' &&
                index + 1 < raw.Length &&
                CouldStartRichTextTag(raw[index + 1]))
            {
                safe.Append('\u200B');
            }
        }

        return safe.ToString();
    }

    private static bool CouldStartRichTextTag(char character) =>
        char.IsLetter(character) ||
        character == '/' ||
        character == '#' ||
        character == '!' ||
        character == '?';

    private static decimal ParseReceived(string received)
    {
        if (decimal.TryParse(
                received,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out decimal amount))
        {
            return amount;
        }

        throw new FormatException("Supporter received amount is not a canonical decimal value.");
    }

    private sealed class RollupBuilder
    {
        private readonly List<SupporterTransaction> _transactions = new List<SupporterTransaction>();
        private decimal _totalReceived;
        private int _messageCount;

        public RollupBuilder(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public void Add(SupporterTransaction transaction)
        {
            _transactions.Add(transaction);
            _totalReceived += ParseReceived(transaction.Received);
            if (!string.IsNullOrEmpty(transaction.Message))
            {
                _messageCount++;
            }
        }

        public SupporterIdentityRollup Build() =>
            new SupporterIdentityRollup(
                Name,
                _totalReceived,
                _transactions.ToArray(),
                _messageCount);
    }
}

#nullable restore
