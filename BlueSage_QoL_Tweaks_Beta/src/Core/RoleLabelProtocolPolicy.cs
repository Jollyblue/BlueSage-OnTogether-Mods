using System;
using System.Globalization;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum RoleLabelProtocolAction
{
    Set,
    Clear
}

internal readonly struct RoleLabelProtocolMessage
{
    internal RoleLabelProtocolMessage(
        RoleLabelProtocolAction action,
        long issuedUnixSeconds,
        string nonce,
        string label)
    {
        Action = action;
        IssuedUnixSeconds = issuedUnixSeconds;
        Nonce = nonce ?? string.Empty;
        Label = label ?? string.Empty;
    }

    internal RoleLabelProtocolAction Action { get; }
    internal long IssuedUnixSeconds { get; }
    internal string Nonce { get; }
    internal string Label { get; }
}

internal static class RoleLabelProtocolPolicy
{
    internal const int FreshnessSeconds = 8;
    internal const int MaxLabelCharacters = 96;
    private const int MaxPayloadCharacters = 640;
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    internal static bool TryBuildSet(
        string label,
        long issuedUnixSeconds,
        string nonce,
        out string payload,
        out string error)
    {
        payload = string.Empty;
        if (!TryValidateEnvelope(issuedUnixSeconds, nonce, out error) ||
            !TryValidateLabel(label, out string clean, out error))
        {
            return false;
        }

        string encoded = EncodeBase64Url(StrictUtf8.GetBytes(clean));
        payload = string.Join(
            "|",
            "v1",
            "set",
            issuedUnixSeconds.ToString(CultureInfo.InvariantCulture),
            nonce,
            encoded);
        if (payload.Length > MaxPayloadCharacters)
        {
            payload = string.Empty;
            error = "Role label payload exceeds the bounded transport size.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static bool TryBuildClear(
        long issuedUnixSeconds,
        string nonce,
        out string payload,
        out string error)
    {
        payload = string.Empty;
        if (!TryValidateEnvelope(issuedUnixSeconds, nonce, out error))
        {
            return false;
        }

        payload = string.Join(
            "|",
            "v1",
            "clear",
            issuedUnixSeconds.ToString(CultureInfo.InvariantCulture),
            nonce,
            "-");
        error = string.Empty;
        return true;
    }

    internal static bool TryParse(
        string payload,
        long nowUnixSeconds,
        out RoleLabelProtocolMessage message,
        out string error)
    {
        message = default;
        error = "Role label payload is invalid.";
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaxPayloadCharacters)
        {
            return false;
        }

        string[] parts = payload.Split('|');
        if (parts.Length != 5 || !string.Equals(parts[0], "v1", StringComparison.Ordinal) ||
            !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long issuedUnixSeconds) ||
            !TryValidateEnvelope(issuedUnixSeconds, parts[3], out error))
        {
            return false;
        }

        long age = nowUnixSeconds - issuedUnixSeconds;
        if (age < -2 || age > FreshnessSeconds)
        {
            error = "Role label payload is outside the freshness window.";
            return false;
        }

        if (string.Equals(parts[1], "clear", StringComparison.Ordinal))
        {
            if (!string.Equals(parts[4], "-", StringComparison.Ordinal))
            {
                error = "Clear payload must not contain a label.";
                return false;
            }

            message = new RoleLabelProtocolMessage(
                RoleLabelProtocolAction.Clear,
                issuedUnixSeconds,
                parts[3],
                string.Empty);
            error = string.Empty;
            return true;
        }

        if (!string.Equals(parts[1], "set", StringComparison.Ordinal) ||
            !TryDecodeBase64Url(parts[4], out string label) ||
            !TryValidateLabel(label, out string clean, out error))
        {
            return false;
        }

        message = new RoleLabelProtocolMessage(
            RoleLabelProtocolAction.Set,
            issuedUnixSeconds,
            parts[3],
            clean);
        error = string.Empty;
        return true;
    }

    internal static bool TryValidateLabel(string label, out string clean, out string error)
    {
        clean = label ?? string.Empty;
        error = string.Empty;
        if (clean.Length < 1 || clean.Length > MaxLabelCharacters)
        {
            error = "Role label must be between 1 and 96 characters.";
            return false;
        }

        if (clean.IndexOf('\r') >= 0 || clean.IndexOf('\n') >= 0 || clean.IndexOf('\0') >= 0)
        {
            error = "Role label must stay on one line.";
            return false;
        }

        if (!RichTextDraftPolicy.TryValidate(clean, out string richTextError))
        {
            error = "Role label rich text is invalid: " + richTextError;
            return false;
        }

        return true;
    }

    private static bool TryValidateEnvelope(long issuedUnixSeconds, string nonce, out string error)
    {
        if (issuedUnixSeconds <= 0)
        {
            error = "Role label timestamp is invalid.";
            return false;
        }

        if (string.IsNullOrEmpty(nonce) || nonce.Length < 4 || nonce.Length > 16)
        {
            error = "Role label nonce must be 4-16 alphanumeric characters.";
            return false;
        }

        foreach (char character in nonce)
        {
            if (!char.IsLetterOrDigit(character))
            {
                error = "Role label nonce must be alphanumeric.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string EncodeBase64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeBase64Url(string encoded, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrEmpty(encoded) || encoded.Length > 512)
        {
            return false;
        }

        foreach (char character in encoded)
        {
            if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
            {
                return false;
            }
        }

        string padded = encoded.Replace('-', '+').Replace('_', '/');
        int remainder = padded.Length % 4;
        if (remainder == 1)
        {
            return false;
        }
        if (remainder > 0)
        {
            padded = padded.PadRight(padded.Length + (4 - remainder), '=');
        }

        try
        {
            value = StrictUtf8.GetString(Convert.FromBase64String(padded));
            return true;
        }
        catch
        {
            value = string.Empty;
            return false;
        }
    }
}
