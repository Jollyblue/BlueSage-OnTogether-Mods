Set-StrictMode -Version Latest

if (-not ('BlueSage.PackagePrivacyScanner' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BlueSage
{
    public static class PackagePrivacyScanner
    {
        public static bool ContainsForbiddenDigest(string text, string[] forbiddenDigests)
        {
            if (String.IsNullOrEmpty(text) || forbiddenDigests == null || forbiddenDigests.Length == 0)
            {
                return false;
            }

            List<byte[]> forbidden = new List<byte[]>(forbiddenDigests.Length);
            for (int index = 0; index < forbiddenDigests.Length; index++)
            {
                forbidden.Add(ParseHexDigest(forbiddenDigests[index]));
            }
            MatchCollection matches = Regex.Matches(
                NormalizeSafely(text),
                @"[\p{L}\p{Nd}]+",
                RegexOptions.CultureInvariant);
            if (matches.Count == 0)
            {
                return false;
            }

            string[] tokens = new string[matches.Count];
            for (int index = 0; index < matches.Count; index++)
            {
                tokens[index] = matches[index].Value;
            }

            using (SHA256 sha = SHA256.Create())
            {
                for (int start = 0; start < tokens.Length; start++)
                {
                    StringBuilder compactBuilder = new StringBuilder();
                    for (int length = 1; length <= 4 && start + length <= tokens.Length; length++)
                    {
                        compactBuilder.Append(tokens[start + length - 1]);
                        string spaced = String.Join(" ", tokens, start, length);
                        string compact = compactBuilder.ToString();
                        if (MatchesAnyForm(sha, spaced, forbidden) ||
                            (!String.Equals(spaced, compact, StringComparison.Ordinal) &&
                             MatchesAnyForm(sha, compact, forbidden)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string NormalizeSafely(string text)
        {
            StringBuilder safe = new StringBuilder(text.Length);
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (Char.IsHighSurrogate(current))
                {
                    if (index + 1 < text.Length && Char.IsLowSurrogate(text[index + 1]))
                    {
                        safe.Append(current);
                        safe.Append(text[++index]);
                    }
                    else
                    {
                        safe.Append(' ');
                    }
                }
                else if (Char.IsLowSurrogate(current))
                {
                    safe.Append(' ');
                }
                else
                {
                    safe.Append(current);
                }
            }

            string sanitized = safe.ToString();
            try
            {
                return sanitized.Normalize(NormalizationForm.FormKC);
            }
            catch (ArgumentException)
            {
                return sanitized;
            }
        }

        private static byte[] ParseHexDigest(string value)
        {
            byte[] result = new byte[value.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            }

            return result;
        }

        private static bool MatchesAnyForm(SHA256 sha, string candidate, List<byte[]> forbidden)
        {
            if (candidate.Length == 0 || candidate.Length > 256)
            {
                return false;
            }

            if (Matches(sha, candidate, forbidden))
            {
                return true;
            }

            string lower = candidate.ToLowerInvariant();
            if (!String.Equals(candidate, lower, StringComparison.Ordinal) &&
                Matches(sha, lower, forbidden))
            {
                return true;
            }

            string upper = candidate.ToUpperInvariant();
            return !String.Equals(candidate, upper, StringComparison.Ordinal) &&
                   !String.Equals(lower, upper, StringComparison.Ordinal) &&
                   Matches(sha, upper, forbidden);
        }

        private static bool Matches(SHA256 sha, string candidate, List<byte[]> forbidden)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(candidate);
            byte[] digest = sha.ComputeHash(bytes);
            for (int digestIndex = 0; digestIndex < forbidden.Count; digestIndex++)
            {
                byte[] expected = forbidden[digestIndex];
                bool equal = digest.Length == expected.Length;
                for (int byteIndex = 0; equal && byteIndex < digest.Length; byteIndex++)
                {
                    equal = digest[byteIndex] == expected[byteIndex];
                }

                if (equal)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
'@
}

function Test-BlueSagePrivacyText {
    param(
        [AllowEmptyString()]
        [string]$Text,

        [Parameter(Mandatory)]
        [System.Collections.Generic.HashSet[string]]$ForbiddenDigestSet
    )

    if ([string]::IsNullOrEmpty($Text) -or $ForbiddenDigestSet.Count -eq 0) {
        return $false
    }

    [string[]]$digests = @($ForbiddenDigestSet)
    return [BlueSage.PackagePrivacyScanner]::ContainsForbiddenDigest($Text, $digests)
}

function Get-BlueSagePrintableAsciiText {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes
    )

    $characters = [char[]]::new($Bytes.Length)
    for ($index = 0; $index -lt $Bytes.Length; $index++) {
        $value = $Bytes[$index]
        $characters[$index] = if ($value -ge 0x20 -and $value -le 0x7e) {
            [char]$value
        }
        else {
            ' '
        }
    }

    return [string]::new($characters)
}

function Test-BlueSagePrivacyBytes {
    param(
        [Parameter(Mandatory)]
        [byte[]]$Bytes,

        [Parameter(Mandatory)]
        [System.Collections.Generic.HashSet[string]]$ForbiddenDigestSet
    )

    $candidateTexts = [System.Collections.Generic.List[string]]::new()
    $candidateTexts.Add([System.Text.Encoding]::UTF8.GetString($Bytes))
    $candidateTexts.Add((Get-BlueSagePrintableAsciiText -Bytes $Bytes))

    foreach ($offset in @(0, 1)) {
        $available = $Bytes.Length - $offset
        $evenLength = $available - ($available % 2)
        if ($evenLength -le 0) {
            continue
        }

        $candidateTexts.Add([System.Text.Encoding]::Unicode.GetString($Bytes, $offset, $evenLength))
    }

    foreach ($candidateText in $candidateTexts) {
        if (Test-BlueSagePrivacyText -Text $candidateText -ForbiddenDigestSet $ForbiddenDigestSet) {
            return $true
        }
    }

    return $false
}

function Get-BlueSageZipEntryBytes {
    param(
        [Parameter(Mandatory)]
        [System.IO.Compression.ZipArchiveEntry]$Entry
    )

    $stream = $Entry.Open()
    try {
        $buffer = [System.IO.MemoryStream]::new()
        try {
            $stream.CopyTo($buffer)
            return [byte[]]$buffer.ToArray()
        }
        finally {
            $buffer.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Test-BlueSageZipPrivacy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ZipPath,

        [Parameter(Mandatory)]
        [string[]]$ForbiddenDigests,

        [long]$MaximumEntryBytes = 64L * 1024L * 1024L
    )

    $digestSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($digest in $ForbiddenDigests) {
        $normalized = if ($null -eq $digest) { '' } else { $digest.Trim().ToUpperInvariant() }
        if ($normalized -notmatch '\A[0-9A-F]{64}\z') {
            throw 'A package privacy digest is not a valid SHA-256 value.'
        }

        [void]$digestSet.Add($normalized)
    }

    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw 'Package privacy scan could not find the requested ZIP.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $findingCount = 0
    $scanErrorCount = 0
    $entryCount = 0
    $archive = [System.IO.Compression.ZipFile]::OpenRead([System.IO.Path]::GetFullPath($ZipPath))
    try {
        foreach ($entry in $archive.Entries) {
            $entryCount++
            if (Test-BlueSagePrivacyText -Text $entry.FullName -ForbiddenDigestSet $digestSet) {
                $findingCount++
            }

            if ([string]::IsNullOrEmpty($entry.Name)) {
                continue
            }

            if ($entry.Length -lt 0 -or $entry.Length -gt $MaximumEntryBytes) {
                $scanErrorCount++
                continue
            }

            try {
                $entryBytes = Get-BlueSageZipEntryBytes -Entry $entry
                if (Test-BlueSagePrivacyBytes -Bytes $entryBytes -ForbiddenDigestSet $digestSet) {
                    $findingCount++
                }
            }
            catch {
                $scanErrorCount++
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    return [pscustomobject]@{
        Passed = ($findingCount -eq 0 -and $scanErrorCount -eq 0)
        FindingCount = $findingCount
        ScanErrorCount = $scanErrorCount
        ScannedEntryCount = $entryCount
    }
}

Export-ModuleMember -Function Test-BlueSageZipPrivacy
