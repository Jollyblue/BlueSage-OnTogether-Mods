using System;

namespace BlueSage.QoLTweaks.Core;

internal enum IdentityResyncAccess
{
    Ordinary,
    Host,
    HelperReady,
    HelperWaitingForHost,
}

internal enum IdentityResyncAction
{
    Denied,
    Status,
    RefreshLocalSurfaces,
    RepairIncompleteRows,
    PublishHostRequest,
}

internal sealed class IdentityResyncPlan
{
    internal IdentityResyncPlan(
        bool allowed,
        IdentityResyncAction action,
        bool readOnly,
        bool requiresAuthoritativeCapture,
        string reason)
    {
        Allowed = allowed;
        Action = action;
        ReadOnly = readOnly;
        RequiresAuthoritativeCapture = requiresAuthoritativeCapture;
        Reason = reason ?? string.Empty;
    }

    internal bool Allowed { get; }
    internal IdentityResyncAction Action { get; }
    internal bool ReadOnly { get; }
    internal bool RequiresAuthoritativeCapture { get; }
    internal bool MayWriteNativeIdentity => false;
    internal string Reason { get; }
}

internal static class IdentityResyncPlanPolicy
{
    internal static IdentityResyncPlan Plan(
        string commandName,
        string arguments,
        IdentityResyncAccess access)
    {
        string normalizedName = Normalize(commandName);
        if (normalizedName != "idsync" &&
            normalizedName != "identitysync" &&
            normalizedName != "rostersync")
        {
            return Denied("That command is not an identity-resync command.");
        }

        string[] tokens = (arguments ?? string.Empty)
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string action = tokens.Length == 0
            ? "status"
            : tokens[0].Trim().ToLowerInvariant();
        if (tokens.Length > 1)
        {
            return Denied("Identity resync accepts one operation only: status, refresh, repair, or request.");
        }

        if (action == "status" || action == "?" || action == "help")
        {
            return new IdentityResyncPlan(
                allowed: true,
                IdentityResyncAction.Status,
                readOnly: true,
                requiresAuthoritativeCapture: false,
                "Read-only identity agreement status.");
        }

        if (action == "refresh")
        {
            return new IdentityResyncPlan(
                allowed: true,
                IdentityResyncAction.RefreshLocalSurfaces,
                readOnly: false,
                requiresAuthoritativeCapture: false,
                "Refresh only BlueSage-owned local presentation from already available native state.");
        }

        if (action == "repair")
        {
            return access == IdentityResyncAccess.Host
                ? new IdentityResyncPlan(
                    allowed: true,
                    IdentityResyncAction.RepairIncompleteRows,
                    readOnly: false,
                    requiresAuthoritativeCapture: true,
                    "Verified current Host may run one bounded exact-correlation repair.")
                : Denied("Direct identity repair requires the verified current Host.");
        }

        if (action == "request")
        {
            return access == IdentityResyncAccess.HelperReady
                ? new IdentityResyncPlan(
                    allowed: true,
                    IdentityResyncAction.PublishHostRequest,
                    readOnly: false,
                    requiresAuthoritativeCapture: false,
                    "Authenticated Helper may publish one generation-bound request for Host revalidation.")
                : Denied(
                    access == IdentityResyncAccess.HelperWaitingForHost
                        ? "Identity repair request is locked until the current Host capability is fresh."
                        : "Identity repair request requires a Helper authenticated by the current Host.");
        }

        return Denied("Identity resync accepts status, refresh, repair, or request.");
    }

    private static IdentityResyncPlan Denied(string reason)
    {
        return new IdentityResyncPlan(
            allowed: false,
            IdentityResyncAction.Denied,
            readOnly: true,
            requiresAuthoritativeCapture: false,
            reason);
    }

    private static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .Trim()
            .TrimStart('/')
            .ToLowerInvariant();
    }
}

internal sealed class IdentityResyncCoverage
{
    internal IdentityResyncCoverage(
        int verifiedRows,
        int nativeRows,
        int purrNetRows,
        int steamMembers,
        int liveNativeOnlyRows,
        int leftStaleRows,
        int ambiguousRows,
        bool captureAvailable,
        string errorReason)
        : this(
            verifiedRows,
            nativeRows,
            nativeRows,
            purrNetRows,
            steamMembers,
            liveNativeOnlyRows,
            leftStaleRows,
            ambiguousRows,
            captureAvailable,
            errorReason)
    {
    }

    internal IdentityResyncCoverage(
        int verifiedRows,
        int nativeRows,
        int rawNativeRows,
        int purrNetRows,
        int steamMembers,
        int liveNativeOnlyRows,
        int leftStaleRows,
        int ambiguousRows,
        bool captureAvailable,
        string errorReason)
    {
        VerifiedRows = Math.Max(0, verifiedRows);
        NativeRows = Math.Max(0, nativeRows);
        RawNativeRows = Math.Max(NativeRows, rawNativeRows);
        ExcludedNativeRows = Math.Max(0, RawNativeRows - NativeRows);
        PurrNetRows = Math.Max(0, purrNetRows);
        SteamMembers = Math.Max(0, steamMembers);
        LiveNativeOnlyRows = Math.Max(0, liveNativeOnlyRows);
        LeftStaleRows = Math.Max(0, leftStaleRows);
        AmbiguousRows = Math.Max(0, ambiguousRows);
        CaptureAvailable = captureAvailable;
        ErrorReason = errorReason ?? string.Empty;

        int unresolvedNative = Math.Max(0, NativeRows - VerifiedRows);
        int unresolvedPurrNet = Math.Max(0, PurrNetRows - VerifiedRows);
        int unresolvedSteam = Math.Max(0, SteamMembers - VerifiedRows);
        LockedRows = Math.Max(
            Math.Max(unresolvedNative, unresolvedPurrNet),
            Math.Max(
                unresolvedSteam,
                LiveNativeOnlyRows + AmbiguousRows));

        Ready =
            CaptureAvailable &&
            string.IsNullOrWhiteSpace(ErrorReason) &&
            VerifiedRows == NativeRows &&
            NativeRows == PurrNetRows &&
            PurrNetRows == SteamMembers &&
            LiveNativeOnlyRows == 0 &&
            AmbiguousRows == 0;
        Reason = BuildReason();
    }

    internal int VerifiedRows { get; }
    internal int NativeRows { get; }
    internal int RawNativeRows { get; }
    internal int ExcludedNativeRows { get; }
    internal int PurrNetRows { get; }
    internal int SteamMembers { get; }
    internal int LiveNativeOnlyRows { get; }
    internal int LeftStaleRows { get; }
    internal int AmbiguousRows { get; }
    internal int LockedRows { get; }
    internal bool CaptureAvailable { get; }
    internal bool Ready { get; }
    internal string ErrorReason { get; }
    internal string Reason { get; }

    internal static IdentityResyncCoverage Unavailable(string reason)
    {
        return new IdentityResyncCoverage(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            captureAvailable: false,
            reason);
    }

    private string BuildReason()
    {
        if (!CaptureAvailable)
        {
            return string.IsNullOrWhiteSpace(ErrorReason)
                ? "no authoritative identity snapshot is available yet"
                : ErrorReason;
        }
        if (!string.IsNullOrWhiteSpace(ErrorReason))
        {
            return ErrorReason;
        }
        if (NativeRows > SteamMembers || PurrNetRows > SteamMembers)
        {
            return "native/PurrNet rows exceed current Steam membership; unmatched rows remain locked";
        }
        if (SteamMembers > NativeRows || SteamMembers > PurrNetRows)
        {
            return "current Steam members are still waiting for exact native/PurrNet rows";
        }
        if (AmbiguousRows > 0)
        {
            return "ambiguous native/PurrNet rows remain locked";
        }
        if (LiveNativeOnlyRows > 0)
        {
            return "live native-only rows remain outside current Steam membership and locked";
        }
        if (Ready && LeftStaleRows > 0)
        {
            return LeftStaleRows == 1
                ? "exact Steam/PurrNet/native-row coverage is ready; 1 stale native row excluded pending game cleanup"
                : "exact Steam/PurrNet/native-row coverage is ready; " +
                  LeftStaleRows +
                  " stale native rows excluded pending game cleanup";
        }
        return Ready
            ? "exact Steam/PurrNet/native-row coverage is ready"
            : "exact Steam/PurrNet/native-row coverage is incomplete";
    }
}
