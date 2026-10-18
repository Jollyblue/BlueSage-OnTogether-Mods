using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal sealed class ScopedSenderEvidenceContext
{
    internal bool HasSenderBinding { get; set; }
    internal bool UniqueCurrentAgreement { get; set; }
    internal bool FreshAtClick { get; set; }
    internal bool ExactTupleMatches { get; set; }
    internal bool PrivilegedObserver { get; set; }
    internal string SteamId { get; set; } = string.Empty;
    internal string PlayerId { get; set; } = string.Empty;
    internal int NativeRowIndex { get; set; } = -1;
    internal string SessionRowCorrelator { get; set; } = string.Empty;
}

internal static class ScopedSenderEvidencePolicy
{
    private const int MaximumEvidenceCharacters = 1024;

    internal static bool TryBuild(
        ScopedSenderEvidenceContext context,
        out string evidence,
        out string error)
    {
        evidence = string.Empty;
        error = string.Empty;

        if (context == null ||
            !context.HasSenderBinding ||
            !context.UniqueCurrentAgreement ||
            !context.FreshAtClick ||
            !context.ExactTupleMatches)
        {
            error =
                "Exact identity evidence stayed locked because the current " +
                "Steam/PurrNet/native-row tuple did not agree uniquely.";
            return false;
        }

        if (!IsSteamId64(context.SteamId) ||
            !IsBoundedField(context.PlayerId, 128) ||
            context.NativeRowIndex < 0 ||
            !IsCorrelator(context.SessionRowCorrelator))
        {
            error =
                "Exact identity evidence stayed locked because one captured " +
                "tuple field was malformed.";
            return false;
        }

        var builder = new StringBuilder(320);
        builder.AppendLine(
            "BlueSage exact identity evidence - EVIDENCE ONLY");
        builder.AppendLine(
            "Local clipboard only. Revalidate the current native row before any action.");
        builder.Append("Native row index (zero-based): ")
            .Append(context.NativeRowIndex)
            .AppendLine();
        builder.Append("Session row correlator: ")
            .Append(context.SessionRowCorrelator)
            .AppendLine();
        builder.Append("SteamID64: ")
            .Append(context.SteamId)
            .AppendLine();
        if (context.PrivilegedObserver)
        {
            builder.Append("PurrNet PlayerID: ")
                .Append(context.PlayerId)
                .AppendLine();
        }

        builder.Append(
            "Agreement: unique current Steam/PurrNet/native-row/transform");
        evidence = builder.ToString().TrimEnd();
        if (evidence.Length > MaximumEvidenceCharacters)
        {
            evidence = string.Empty;
            error =
                "Exact identity evidence stayed locked because the bounded " +
                "clipboard record was too large.";
            return false;
        }

        return true;
    }

    private static bool IsSteamId64(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 17)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] < '0' || value[index] > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBoundedField(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > maximumLength)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsControl(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCorrelator(string value)
    {
        const string prefix = "row-";
        if (string.IsNullOrEmpty(value) ||
            value.Length != prefix.Length + 12 ||
            !value.StartsWith(prefix))
        {
            return false;
        }

        for (int index = prefix.Length; index < value.Length; index++)
        {
            char character = value[index];
            bool hex =
                (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f');
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }
}
