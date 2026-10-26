using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum LegacyConfigAction
{
    Keep,
    RecordHash,
    Reimport
}

// Round 4 review R2 (2026-10-19): the old qol-avatar-styles.cfg stays on disk as the 0.2.5 rollback copy. A player
// who rolls back edits slots through that file; on re-upgrade those edits must not be dropped. The store records
// the content hash of what it imported, and a changed .cfg wins (Fable amendment, option a) after the current
// store is copied aside. The hash covers values, not bytes, so a rewrite of the same settings never re-imports.
internal static class LegacyConfigReimportPolicy
{
    // recordedHash: null = a v1 store (unknown), "" = the .cfg was absent when recorded. currentHash: null = absent now.
    internal static LegacyConfigAction Decide(string recordedHash, string currentHash, bool legacyHasAnyPayload)
    {
        if (recordedHash == null)
        {
            // A v1 store never re-imports: it was created from this .cfg and has edits the .cfg lacks.
            return LegacyConfigAction.RecordHash;
        }

        if (currentHash == null)
        {
            return recordedHash.Length == 0 ? LegacyConfigAction.Keep : LegacyConfigAction.RecordHash;
        }

        if (string.Equals(recordedHash, currentHash, StringComparison.Ordinal))
        {
            return LegacyConfigAction.Keep;
        }

        // An emptied .cfg holds no creative work; recording it keeps the next real change detectable.
        return legacyHasAnyPayload ? LegacyConfigAction.Reimport : LegacyConfigAction.RecordHash;
    }

    internal static bool HasAnyPayload(IReadOnlyList<string> payloads)
    {
        for (int index = 0; index < payloads.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(payloads[index]))
            {
                return true;
            }
        }

        return false;
    }

    // Names and payloads only: merely clicking a different slot under a rollback rewrites SelectedExtendedSlot in the
    // .cfg, and that is not an edit worth replacing the store for. A real re-import still takes the selection from the .cfg.
    internal static string ContentHash(IReadOnlyList<string> names, IReadOnlyList<string> payloads)
    {
        StringBuilder canonical = new StringBuilder();
        int count = Math.Min(names.Count, payloads.Count);
        for (int index = 0; index < count; index++)
        {
            canonical.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(names[index] ?? string.Empty))).Append('\n');
            canonical.Append(payloads[index] ?? string.Empty).Append('\n');
        }

        using (SHA256 sha = SHA256.Create())
        {
            byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            return BitConverter.ToString(digest).Replace("-", string.Empty);
        }
    }
}
