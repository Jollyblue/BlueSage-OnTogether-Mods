using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal enum PlayerBadgeKind
{
    Host,
    Helper,
    CompatibleClient,
    ProtectedVictim,
    Suspect,
    Supporter,
    Creator,
}

internal readonly struct PlayerBadgeEvidence
{
    internal PlayerBadgeEvidence(
        bool uniqueExactRow,
        bool uniqueCurrentMember,
        bool ownerMatchesRow,
        bool nativeHostAgreement,
        bool moderationCapabilityFresh,
        bool ownerListsRowAsHelper,
        bool compatibleHeartbeatFresh,
        bool cloneFeatureEnabled,
        bool localSafetyAuthorized,
        bool openVictim,
        bool openSuspect,
        bool creatorContributionFresh,
        bool supporterIdentityHashMatched = false,
        bool ownerPublishedCreatorHostLabelFresh = false,
        bool compatiblePresentationRow = false)
    {
        UniqueExactRow = uniqueExactRow;
        UniqueCurrentMember = uniqueCurrentMember;
        OwnerMatchesRow = ownerMatchesRow;
        NativeHostAgreement = nativeHostAgreement;
        ModerationCapabilityFresh = moderationCapabilityFresh;
        OwnerListsRowAsHelper = ownerListsRowAsHelper;
        CompatibleHeartbeatFresh = compatibleHeartbeatFresh;
        CloneFeatureEnabled = cloneFeatureEnabled;
        LocalSafetyAuthorized = localSafetyAuthorized;
        OpenVictim = openVictim;
        OpenSuspect = openSuspect;
        CreatorContributionFresh = creatorContributionFresh;
        SupporterIdentityHashMatched = supporterIdentityHashMatched;
        OwnerPublishedCreatorHostLabelFresh = ownerPublishedCreatorHostLabelFresh;
        CompatiblePresentationRow = compatiblePresentationRow;
    }

    internal bool UniqueExactRow { get; }
    internal bool UniqueCurrentMember { get; }
    internal bool OwnerMatchesRow { get; }
    internal bool NativeHostAgreement { get; }
    internal bool ModerationCapabilityFresh { get; }
    internal bool OwnerListsRowAsHelper { get; }
    internal bool CompatibleHeartbeatFresh { get; }
    internal bool CloneFeatureEnabled { get; }
    internal bool LocalSafetyAuthorized { get; }
    internal bool OpenVictim { get; }
    internal bool OpenSuspect { get; }
    internal bool CreatorContributionFresh { get; }
    internal bool SupporterIdentityHashMatched { get; }
    internal bool OwnerPublishedCreatorHostLabelFresh { get; }
    // A visible [U] is a read-only, self-published compatibility hint. It may
    // remain visible for a currently bound native row while unrelated roster
    // drift keeps identity-sensitive actions fail-closed.
    internal bool CompatiblePresentationRow { get; }

    internal PlayerBadgeEvidence With(
        bool? uniqueExactRow = null,
        bool? uniqueCurrentMember = null,
        bool? ownerMatchesRow = null,
        bool? nativeHostAgreement = null,
        bool? moderationCapabilityFresh = null,
        bool? ownerListsRowAsHelper = null,
        bool? compatibleHeartbeatFresh = null,
        bool? cloneFeatureEnabled = null,
        bool? localSafetyAuthorized = null,
        bool? openVictim = null,
        bool? openSuspect = null,
        bool? creatorContributionFresh = null,
        bool? supporterIdentityHashMatched = null,
        bool? ownerPublishedCreatorHostLabelFresh = null,
        bool? compatiblePresentationRow = null)
    {
        return new PlayerBadgeEvidence(
            uniqueExactRow ?? UniqueExactRow,
            uniqueCurrentMember ?? UniqueCurrentMember,
            ownerMatchesRow ?? OwnerMatchesRow,
            nativeHostAgreement ?? NativeHostAgreement,
            moderationCapabilityFresh ?? ModerationCapabilityFresh,
            ownerListsRowAsHelper ?? OwnerListsRowAsHelper,
            compatibleHeartbeatFresh ?? CompatibleHeartbeatFresh,
            cloneFeatureEnabled ?? CloneFeatureEnabled,
            localSafetyAuthorized ?? LocalSafetyAuthorized,
            openVictim ?? OpenVictim,
            openSuspect ?? OpenSuspect,
            creatorContributionFresh ?? CreatorContributionFresh,
            supporterIdentityHashMatched ?? SupporterIdentityHashMatched,
            ownerPublishedCreatorHostLabelFresh ?? OwnerPublishedCreatorHostLabelFresh,
            compatiblePresentationRow ?? CompatiblePresentationRow);
    }
}

internal sealed class PlayerBadgePresentation
{
    internal PlayerBadgePresentation(
        IReadOnlyList<PlayerBadgeKind> allBadges,
        string displayText,
        string tooltipText,
        int hiddenCount)
    {
        AllBadges = allBadges ?? Array.Empty<PlayerBadgeKind>();
        DisplayText = displayText ?? string.Empty;
        TooltipText = tooltipText ?? string.Empty;
        HiddenCount = Math.Max(0, hiddenCount);
    }

    internal IReadOnlyList<PlayerBadgeKind> AllBadges { get; }
    internal string DisplayText { get; }
    internal string TooltipText { get; }
    internal int HiddenCount { get; }
}

internal static class PlayerBadgePolicy
{
    private static readonly PlayerBadgeKind[] OrderedBadges =
    {
        PlayerBadgeKind.Host,
        PlayerBadgeKind.Helper,
        PlayerBadgeKind.CompatibleClient,
        PlayerBadgeKind.ProtectedVictim,
        PlayerBadgeKind.Suspect,
        PlayerBadgeKind.Supporter,
        PlayerBadgeKind.Creator,
    };

    private static readonly PlayerBadgeKind[] OverflowPriority =
    {
        PlayerBadgeKind.Host,
        PlayerBadgeKind.Helper,
        PlayerBadgeKind.Creator,
        PlayerBadgeKind.Supporter,
        PlayerBadgeKind.CompatibleClient,
        PlayerBadgeKind.ProtectedVictim,
        PlayerBadgeKind.Suspect,
    };

    internal static IReadOnlyList<PlayerBadgeKind> PresentationOrder => OrderedBadges;

    internal static PlayerBadgePresentation Compose(
        PlayerBadgeEvidence evidence,
        int capacity)
    {
        var badges = new List<PlayerBadgeKind>(OrderedBadges.Length);
        bool exactCurrentRow = evidence.UniqueExactRow && evidence.UniqueCurrentMember;

        if (exactCurrentRow &&
            evidence.OwnerMatchesRow &&
            evidence.NativeHostAgreement)
        {
            badges.Add(PlayerBadgeKind.Host);
        }

        if (exactCurrentRow &&
            !evidence.OwnerMatchesRow &&
            !evidence.NativeHostAgreement &&
            evidence.ModerationCapabilityFresh &&
            evidence.OwnerListsRowAsHelper)
        {
            badges.Add(PlayerBadgeKind.Helper);
        }

        if ((exactCurrentRow || evidence.CompatiblePresentationRow) &&
            evidence.CompatibleHeartbeatFresh)
        {
            badges.Add(PlayerBadgeKind.CompatibleClient);
        }

        bool incidentPresentationAllowed =
            exactCurrentRow &&
            evidence.CloneFeatureEnabled &&
            evidence.LocalSafetyAuthorized;
        if (incidentPresentationAllowed && evidence.OpenVictim)
        {
            badges.Add(PlayerBadgeKind.ProtectedVictim);
        }
        if (incidentPresentationAllowed && evidence.OpenSuspect)
        {
            badges.Add(PlayerBadgeKind.Suspect);
        }

        if (exactCurrentRow && evidence.SupporterIdentityHashMatched)
        {
            badges.Add(PlayerBadgeKind.Supporter);
        }

        if (exactCurrentRow &&
            (evidence.CreatorContributionFresh ||
             (evidence.OwnerPublishedCreatorHostLabelFresh &&
              evidence.OwnerMatchesRow &&
              evidence.NativeHostAgreement)))
        {
            badges.Add(PlayerBadgeKind.Creator);
        }

        string tooltip = string.Join(
            "\n",
            badges.Select(Describe));
        return BuildPresentation(
            badges,
            tooltip,
            capacity);
    }

    internal static PlayerBadgePresentation Collapse(
        PlayerBadgePresentation source,
        int capacity)
    {
        if (source == null)
        {
            return new PlayerBadgePresentation(
                Array.Empty<PlayerBadgeKind>(),
                string.Empty,
                string.Empty,
                0);
        }

        return BuildPresentation(
            source.AllBadges,
            source.TooltipText,
            capacity);
    }

    private static PlayerBadgePresentation BuildPresentation(
        IReadOnlyList<PlayerBadgeKind> badges,
        string tooltip,
        int capacity)
    {
        if (badges == null || badges.Count == 0 || capacity <= 0)
        {
            return new PlayerBadgePresentation(
                badges?.ToArray() ?? Array.Empty<PlayerBadgeKind>(),
                string.Empty,
                tooltip,
                badges?.Count ?? 0);
        }

        if (badges.Count <= capacity)
        {
            return new PlayerBadgePresentation(
                badges.ToArray(),
                string.Join(" ", badges.Select(Token)),
                tooltip,
                0);
        }

        int visibleCount = Math.Max(0, capacity - 1);
        int hiddenCount = badges.Count - visibleCount;
        bool hasCompatibleClient = badges.Contains(PlayerBadgeKind.CompatibleClient);
        if (hasCompatibleClient)
        {
            // [U] is a fresh, Steam-member-bound, presentation-only signal.
            // Preserve it even when the row becomes narrow; all authority
            // badges still use exact current identity evidence above.
            if (capacity == 1)
            {
                return new PlayerBadgePresentation(
                    badges.ToArray(),
                    Token(PlayerBadgeKind.CompatibleClient),
                    tooltip,
                    badges.Count - 1);
            }

            int nonCompatibleSlots = capacity == 2
                ? 1
                : Math.Max(0, visibleCount - 1);
            PlayerBadgeKind[] selected = OverflowPriority
                .Where(kind => kind != PlayerBadgeKind.CompatibleClient && badges.Contains(kind))
                .Take(nonCompatibleSlots)
                .Concat(new[] { PlayerBadgeKind.CompatibleClient })
                .ToArray();
            string[] compatibleTokens = OrderedBadges
                .Where(selected.Contains)
                .Select(Token)
                .ToArray();
            int compatibleHiddenCount = badges.Count - compatibleTokens.Length;
            if (capacity > 2 && compatibleHiddenCount > 0)
            {
                compatibleTokens = compatibleTokens
                    .Concat(new[] { "[+" + compatibleHiddenCount + "]" })
                    .ToArray();
            }

            return new PlayerBadgePresentation(
                badges.ToArray(),
                string.Join(" ", compatibleTokens),
                tooltip,
                compatibleHiddenCount);
        }

        PlayerBadgeKind[] prioritized = OverflowPriority
            .Where(badges.Contains)
            .Take(visibleCount)
            .ToArray();
        string[] tokens = prioritized
            .Select(Token)
            .Concat(new[] { "[+" + hiddenCount + "]" })
            .ToArray();
        return new PlayerBadgePresentation(
            badges.ToArray(),
            string.Join(" ", tokens),
            tooltip,
            hiddenCount);
    }

    private static string Token(PlayerBadgeKind badge)
    {
        switch (badge)
        {
            case PlayerBadgeKind.Host:
                return "[O]";
            case PlayerBadgeKind.Helper:
                return "[M]";
            case PlayerBadgeKind.CompatibleClient:
                return "[U]";
            case PlayerBadgeKind.ProtectedVictim:
                return "[V]";
            case PlayerBadgeKind.Suspect:
                return "[S]";
            case PlayerBadgeKind.Supporter:
                return "[♥]";
            case PlayerBadgeKind.Creator:
                return "[♪]";
            default:
                return string.Empty;
        }
    }

    private static string Describe(PlayerBadgeKind badge)
    {
        switch (badge)
        {
            case PlayerBadgeKind.Host:
                return "[O]  Owner / Host";
            case PlayerBadgeKind.Helper:
                return "[M] BlueSage Helper (this lobby)";
            case PlayerBadgeKind.CompatibleClient:
                return "[U]  Compatible QoL User";
            case PlayerBadgeKind.ProtectedVictim:
                return "[V] — Protected clone-incident victim";
            case PlayerBadgeKind.Suspect:
                return "[S] — Open clone-incident suspect";
            case PlayerBadgeKind.Supporter:
                return "[♥] — BlueSage community supporter";
            case PlayerBadgeKind.Creator:
                return "[♪] — BlueSage creator identity";
            default:
                return string.Empty;
        }
    }
}

internal static class SupporterBadgeIdentityPolicy
{
    // No authoritative supporter-to-account mapping is present in this tree.
    // Keep the shipped map empty until Blue provides verified SHA-256 digests.
    private static readonly string[] CompiledIdentityHashes = Array.Empty<string>();

    internal static bool IsRecognizedExactIdentity(string exactIdentity)
    {
        return MatchesCompiledIdentityHash(
            exactIdentity,
            CompiledIdentityHashes);
    }

    internal static bool MatchesCompiledIdentityHash(
        string exactIdentity,
        IEnumerable<string> compiledIdentityHashes)
    {
        if (!IsExactSteamIdentityShape(exactIdentity) ||
            compiledIdentityHashes == null)
        {
            return false;
        }

        string digest = ComputeSha256Hex(exactIdentity);
        foreach (string compiledHash in compiledIdentityHashes)
        {
            if (IsSha256Hex(compiledHash) &&
                string.Equals(
                    digest,
                    compiledHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExactSteamIdentityShape(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 17)
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

    private static bool IsSha256Hex(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool hex = (character >= '0' && character <= '9') ||
                       (character >= 'a' && character <= 'f') ||
                       (character >= 'A' && character <= 'F');
            if (!hex)
            {
                return false;
            }
        }

        return true;
    }

    private static string ComputeSha256Hex(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        byte[] digest;
        using (SHA256 sha256 = SHA256.Create())
        {
            digest = sha256.ComputeHash(bytes);
        }

        var builder = new StringBuilder(digest.Length * 2);
        foreach (byte item in digest)
        {
            builder.Append(item.ToString("X2"));
        }

        return builder.ToString();
    }
}
