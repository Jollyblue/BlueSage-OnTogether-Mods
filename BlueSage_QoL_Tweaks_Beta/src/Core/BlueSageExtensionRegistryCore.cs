#nullable disable

using System;
using System.Reflection;

namespace BlueSage.QoLTweaks.Contracts;

internal readonly struct BlueSageLoadedExtensionV1
{
    internal BlueSageLoadedExtensionV1(
        Assembly ownerAssembly,
        Version version,
        bool loaded)
    {
        OwnerAssembly = ownerAssembly;
        Version = version;
        Loaded = loaded;
    }

    internal Assembly OwnerAssembly { get; }
    internal Version Version { get; }
    internal bool Loaded { get; }
}

internal interface IBlueSageLoadedExtensionCatalogV1
{
    bool TryGetLoaded(
        string pluginGuid,
        out BlueSageLoadedExtensionV1 extension);
}

internal sealed class BlueSageExtensionRegistryCore
{
    private readonly IBlueSageLoadedExtensionCatalogV1 _catalog;
    private IBlueSageBadgeContributionV1 _badgeProvider;
    private string _badgeOwnerGuid = string.Empty;
    private Version _badgeOwnerVersion;
    private Assembly _badgeOwnerAssembly;
    private IBlueSageDiagnosticsProviderV1 _diagnosticsProvider;
    private string _diagnosticsOwnerGuid = string.Empty;
    private Version _diagnosticsOwnerVersion;
    private Assembly _diagnosticsOwnerAssembly;
    private IBlueSageHostCapabilityProviderV1 _hostCapabilityProvider;
    private string _hostCapabilityOwnerGuid = string.Empty;
    private Version _hostCapabilityOwnerVersion;
    private Assembly _hostCapabilityOwnerAssembly;
    private long _registrationGeneration;
    private long _badgeRegistrationGeneration;
    private long _diagnosticsRegistrationGeneration;
    private long _hostCapabilityRegistrationGeneration;
    private long _lobbyEpoch;
    private long _evidenceGeneration;
    private long _badgeLobbyEpoch;
    private long _badgeEvidenceGeneration;

    internal BlueSageExtensionRegistryCore(
        IBlueSageLoadedExtensionCatalogV1 catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    internal BlueSageExtensionRegistrationTokenV1 RegisterBadgeContribution(
        IBlueSageBadgeContributionV1 provider,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion)
    {
        if (_badgeProvider != null ||
            provider == null ||
            string.IsNullOrWhiteSpace(ownerGuid) ||
            ownerVersion == null ||
            abiVersion != BlueSageExtensionAbiV1.Version ||
            !_catalog.TryGetLoaded(
                ownerGuid,
                out BlueSageLoadedExtensionV1 loaded) ||
            !IsExactLoadedOwner(provider, ownerVersion, loaded))
        {
            return default;
        }

        BlueSageExtensionRegistrationTokenV1 token =
            NextRegistrationToken();
        _badgeProvider = provider;
        _badgeOwnerGuid = ownerGuid;
        _badgeOwnerVersion = ownerVersion;
        _badgeOwnerAssembly = provider.GetType().Assembly;
        _badgeRegistrationGeneration = token.Generation;
        return token;
    }

    internal BlueSageExtensionRegistrationTokenV1 RegisterDiagnostics(
        IBlueSageDiagnosticsProviderV1 provider,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion)
    {
        return RegisterDiagnostics(
            provider,
            ownerGuid,
            ownerVersion,
            abiVersion,
            provider?.GetType().Assembly);
    }

    internal BlueSageExtensionRegistrationTokenV1 RegisterDiagnostics(
        IBlueSageDiagnosticsProviderV1 provider,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion,
        Assembly ownerAssembly)
    {
        if (_diagnosticsProvider != null ||
            provider == null ||
            ownerAssembly == null ||
            string.IsNullOrWhiteSpace(ownerGuid) ||
            ownerVersion == null ||
            abiVersion != BlueSageExtensionAbiV1.Version ||
            !_catalog.TryGetLoaded(
                ownerGuid,
                out BlueSageLoadedExtensionV1 loaded) ||
            !IsExactLoadedOwner(
                ownerAssembly,
                ownerVersion,
                loaded))
        {
            return default;
        }

        BlueSageExtensionRegistrationTokenV1 token =
            NextRegistrationToken();
        _diagnosticsProvider = provider;
        _diagnosticsOwnerGuid = ownerGuid;
        _diagnosticsOwnerVersion = ownerVersion;
        _diagnosticsOwnerAssembly = ownerAssembly;
        _diagnosticsRegistrationGeneration =
            token.Generation;
        return token;
    }

    internal BlueSageExtensionRegistrationTokenV1 RegisterHostCapability(
        IBlueSageHostCapabilityProviderV1 provider,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion)
    {
        if (_hostCapabilityProvider != null || provider == null ||
            string.IsNullOrWhiteSpace(ownerGuid) || ownerVersion == null ||
            abiVersion != BlueSageExtensionAbiV1.Version ||
            !_catalog.TryGetLoaded(ownerGuid, out BlueSageLoadedExtensionV1 loaded) ||
            !IsExactLoadedOwner(provider, ownerVersion, loaded))
        {
            return default;
        }

        BlueSageExtensionRegistrationTokenV1 token = NextRegistrationToken();
        _hostCapabilityProvider = provider;
        _hostCapabilityOwnerGuid = ownerGuid;
        _hostCapabilityOwnerVersion = ownerVersion;
        _hostCapabilityOwnerAssembly = provider.GetType().Assembly;
        _hostCapabilityRegistrationGeneration = token.Generation;
        return token;
    }

    internal bool Unregister(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        if (UnregisterBadgeContribution(token))
        {
            return true;
        }

        if (UnregisterDiagnostics(token))
        {
            return true;
        }

        return UnregisterHostCapability(token);
    }

    internal bool UnregisterBadgeContribution(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        if (_badgeProvider == null ||
            !token.IsValid ||
            token.Generation !=
                _badgeRegistrationGeneration)
        {
            return false;
        }

        ClearBadgeProvider();
        return true;
    }

    internal bool UnregisterDiagnostics(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        if (_diagnosticsProvider == null ||
            !token.IsValid ||
            token.Generation !=
                _diagnosticsRegistrationGeneration)
        {
            return false;
        }

        ClearDiagnosticsProvider();
        return true;
    }

    internal bool UnregisterHostCapability(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        if (_hostCapabilityProvider == null || !token.IsValid ||
            token.Generation != _hostCapabilityRegistrationGeneration)
        {
            return false;
        }

        ClearHostCapabilityProvider();
        return true;
    }

    internal void SetEvidenceContext(
        long lobbyEpoch,
        long evidenceGeneration)
    {
        if (lobbyEpoch <= 0 || evidenceGeneration <= 0)
        {
            _lobbyEpoch = 0;
            _evidenceGeneration = 0;
            _badgeLobbyEpoch = 0;
            _badgeEvidenceGeneration = 0;
            return;
        }

        _lobbyEpoch = lobbyEpoch;
        _evidenceGeneration = evidenceGeneration;
        _badgeLobbyEpoch = lobbyEpoch;
        _badgeEvidenceGeneration = evidenceGeneration;
    }

    internal void SetBadgeEvidenceContext(
        long lobbyEpoch,
        long evidenceGeneration)
    {
        if (lobbyEpoch <= 0 || evidenceGeneration <= 0)
        {
            _badgeLobbyEpoch = 0;
            _badgeEvidenceGeneration = 0;
            return;
        }

        _badgeLobbyEpoch = lobbyEpoch;
        _badgeEvidenceGeneration = evidenceGeneration;
    }

    internal bool TryReadBadgeContribution(
        BlueSageBadgeContributionRequestV1 request,
        long nowUtcTicks,
        out BlueSageBadgeContributionResponseV1 response)
    {
        response = default;
        if (_badgeProvider == null ||
            nowUtcTicks <= 0 ||
            request.SteamId == 0 ||
            request.PurrNetId == 0 ||
            request.LobbyEpoch != _badgeLobbyEpoch ||
            request.EvidenceGeneration != _badgeEvidenceGeneration ||
            request.Nonce <= 0 ||
            request.IssuedUtcTicks <= 0 ||
            request.IssuedUtcTicks >
                nowUtcTicks +
                BlueSageExtensionAbiV1.MaximumRequestFutureSkewTicks ||
            nowUtcTicks - request.IssuedUtcTicks >
                BlueSageExtensionAbiV1.MaximumContributionLifetimeTicks ||
            !_catalog.TryGetLoaded(
                _badgeOwnerGuid,
                out BlueSageLoadedExtensionV1 loaded) ||
            !loaded.Loaded ||
            loaded.OwnerAssembly != _badgeOwnerAssembly ||
            loaded.Version == null ||
            !_badgeOwnerVersion.Equals(loaded.Version))
        {
            return false;
        }

        try
        {
            BlueSageBadgeContributionResponseV1 candidate =
                _badgeProvider.GetBadgeContribution(request);
            if (candidate.LobbyEpoch != request.LobbyEpoch ||
                candidate.EvidenceGeneration != request.EvidenceGeneration ||
                candidate.Nonce != request.Nonce ||
                candidate.FreshUntilUtcTicks < nowUtcTicks ||
                candidate.FreshUntilUtcTicks >
                    request.IssuedUtcTicks +
                    BlueSageExtensionAbiV1.MaximumContributionLifetimeTicks)
            {
                return false;
            }

            response = candidate;
            return true;
        }
        catch
        {
            response = default;
            return false;
        }
    }

    internal bool TryReadDiagnostics(
        long nowUtcTicks,
        out BlueSageDiagnosticsSnapshotV1 snapshot)
    {
        snapshot = default;
        if (_diagnosticsProvider == null ||
            nowUtcTicks <= 0 ||
            !_catalog.TryGetLoaded(
                _diagnosticsOwnerGuid,
                out BlueSageLoadedExtensionV1 loaded) ||
            !loaded.Loaded ||
            loaded.OwnerAssembly !=
                _diagnosticsOwnerAssembly ||
            loaded.Version == null ||
            !_diagnosticsOwnerVersion.Equals(
                loaded.Version))
        {
            return false;
        }

        try
        {
            BlueSageDiagnosticsSnapshotV1 candidate =
                _diagnosticsProvider.GetSnapshot(
                    nowUtcTicks);
            if (!IsValidDiagnosticsSnapshot(
                    candidate,
                    nowUtcTicks))
            {
                return false;
            }

            snapshot = candidate;
            return true;
        }
        catch
        {
            snapshot = default;
            return false;
        }
    }

    internal bool TrySetDiagnosticsOverlayVisible(bool visible)
    {
        if (!TryGetLiveDiagnosticsOwner() ||
            !(_diagnosticsProvider is IBlueSageDiagnosticsControlV2 control))
        {
            return false;
        }

        try
        {
            return control.TrySetOverlayVisible(visible);
        }
        catch
        {
            return false;
        }
    }

    internal bool TryReadHostCapability(
        BlueSageHostCapabilityRequestV1 request,
        long nowUtcTicks,
        out BlueSageHostCapabilitySnapshotV1 snapshot)
    {
        snapshot = default;
        if (!IsValidHostRequest(request, nowUtcTicks, requireHost: false) ||
            !TryGetLiveHostCapabilityOwner())
        {
            return false;
        }

        try
        {
            BlueSageHostCapabilitySnapshotV1 candidate =
                _hostCapabilityProvider.GetSnapshot(request);
            if (!Enum.IsDefined(typeof(BlueSageHostCapabilityStateV1), candidate.State) ||
                candidate.State == BlueSageHostCapabilityStateV1.Unavailable ||
                string.IsNullOrWhiteSpace(candidate.Title) || candidate.Title.Length > 48 ||
                string.IsNullOrWhiteSpace(candidate.Summary) || candidate.Summary.Length > 240 ||
                candidate.LobbyEpoch != request.LobbyEpoch ||
                candidate.EvidenceGeneration != request.EvidenceGeneration ||
                candidate.Nonce != request.Nonce ||
                candidate.CapturedUtcTicks <= 0 ||
                candidate.CapturedUtcTicks > nowUtcTicks + BlueSageExtensionAbiV1.MaximumRequestFutureSkewTicks ||
                candidate.FreshUntilUtcTicks < nowUtcTicks ||
                candidate.FreshUntilUtcTicks > candidate.CapturedUtcTicks + BlueSageExtensionAbiV1.MaximumContributionLifetimeTicks)
            {
                return false;
            }

            snapshot = candidate;
            return true;
        }
        catch
        {
            snapshot = default;
            return false;
        }
    }

    internal bool TryCreateHostCapabilityRequest(
        bool isHost,
        long nonce,
        long nowUtcTicks,
        out BlueSageHostCapabilityRequestV1 request)
    {
        request = default;
        if (_lobbyEpoch <= 0 || _evidenceGeneration <= 0 ||
            nonce <= 0 || nowUtcTicks <= 0)
        {
            return false;
        }

        request = new BlueSageHostCapabilityRequestV1(
            _lobbyEpoch,
            _evidenceGeneration,
            isHost,
            nonce,
            nowUtcTicks);
        return true;
    }

    internal bool TryExecuteHostCapability(
        BlueSageHostCapabilityActionV1 action,
        BlueSageHostCapabilityRequestV1 request,
        long nowUtcTicks,
        out BlueSageHostCapabilityReceiptV1 receipt)
    {
        receipt = default;
        if (!Enum.IsDefined(typeof(BlueSageHostCapabilityActionV1), action) ||
            !IsValidHostRequest(request, nowUtcTicks, requireHost: true) ||
            !TryGetLiveHostCapabilityOwner())
        {
            return false;
        }

        try
        {
            BlueSageHostCapabilityReceiptV1 candidate =
                _hostCapabilityProvider.TryExecute(action, request);
            if (candidate.LobbyEpoch != request.LobbyEpoch ||
                candidate.EvidenceGeneration != request.EvidenceGeneration ||
                candidate.Nonce != request.Nonce ||
                candidate.CompletedUtcTicks < request.IssuedUtcTicks ||
                candidate.CompletedUtcTicks > nowUtcTicks + BlueSageExtensionAbiV1.MaximumRequestFutureSkewTicks ||
                string.IsNullOrWhiteSpace(candidate.Message) || candidate.Message.Length > 240)
            {
                return false;
            }

            receipt = candidate;
            return true;
        }
        catch
        {
            receipt = default;
            return false;
        }
    }

    private bool IsValidHostRequest(
        BlueSageHostCapabilityRequestV1 request,
        long nowUtcTicks,
        bool requireHost)
    {
        return _hostCapabilityProvider != null && nowUtcTicks > 0 &&
               (!requireHost || request.IsHost) &&
               request.LobbyEpoch == _lobbyEpoch &&
               request.EvidenceGeneration == _evidenceGeneration &&
               request.Nonce > 0 && request.IssuedUtcTicks > 0 &&
               request.IssuedUtcTicks <= nowUtcTicks + BlueSageExtensionAbiV1.MaximumRequestFutureSkewTicks &&
               nowUtcTicks - request.IssuedUtcTicks <= BlueSageExtensionAbiV1.MaximumContributionLifetimeTicks;
    }

    private bool TryGetLiveHostCapabilityOwner()
    {
        return _hostCapabilityProvider != null &&
               _catalog.TryGetLoaded(_hostCapabilityOwnerGuid, out BlueSageLoadedExtensionV1 loaded) &&
               loaded.Loaded && loaded.OwnerAssembly == _hostCapabilityOwnerAssembly &&
               loaded.Version != null && _hostCapabilityOwnerVersion != null &&
               _hostCapabilityOwnerVersion.Equals(loaded.Version);
    }

    internal bool HasLiveHostCapabilityProvider() =>
        TryGetLiveHostCapabilityOwner();

    private bool TryGetLiveDiagnosticsOwner()
    {
        return _diagnosticsProvider != null &&
               _catalog.TryGetLoaded(
                   _diagnosticsOwnerGuid,
                   out BlueSageLoadedExtensionV1 loaded) &&
               loaded.Loaded &&
               loaded.OwnerAssembly == _diagnosticsOwnerAssembly &&
               loaded.Version != null &&
               _diagnosticsOwnerVersion != null &&
               _diagnosticsOwnerVersion.Equals(loaded.Version);
    }

    internal void Clear()
    {
        ClearBadgeProvider();
        ClearDiagnosticsProvider();
        ClearHostCapabilityProvider();
        _lobbyEpoch = 0;
        _evidenceGeneration = 0;
        _badgeLobbyEpoch = 0;
        _badgeEvidenceGeneration = 0;
    }

    private static bool IsExactLoadedOwner(
        object provider,
        Version requestedVersion,
        BlueSageLoadedExtensionV1 loaded)
    {
        return loaded.Loaded &&
               loaded.OwnerAssembly != null &&
               ReferenceEquals(
                   loaded.OwnerAssembly,
                   provider.GetType().Assembly) &&
               loaded.Version != null &&
               requestedVersion.Equals(loaded.Version);
    }

    private static bool IsExactLoadedOwner(
        Assembly ownerAssembly,
        Version requestedVersion,
        BlueSageLoadedExtensionV1 loaded)
    {
        return loaded.Loaded &&
               loaded.OwnerAssembly != null &&
               ReferenceEquals(
                   loaded.OwnerAssembly,
                   ownerAssembly) &&
               loaded.Version != null &&
               requestedVersion.Equals(loaded.Version);
    }

    private BlueSageExtensionRegistrationTokenV1
        NextRegistrationToken()
    {
        _registrationGeneration++;
        if (_registrationGeneration <= 0)
        {
            _registrationGeneration = 1;
        }

        return new BlueSageExtensionRegistrationTokenV1(
            _registrationGeneration);
    }

    private static bool IsValidDiagnosticsSnapshot(
        BlueSageDiagnosticsSnapshotV1 snapshot,
        long nowUtcTicks)
    {
        if (!Enum.IsDefined(
                typeof(BlueSageDiagnosticsOverlayStateV1),
                snapshot.OverlayState) ||
            !Enum.IsDefined(
                typeof(BlueSageDiagnosticsFreshnessV1),
                snapshot.Freshness) ||
            snapshot.IntervalMilliseconds <
                BlueSageDiagnosticsPolicyV1
                    .MinimumIntervalMilliseconds ||
            snapshot.IntervalMilliseconds >
                BlueSageDiagnosticsPolicyV1
                    .MaximumIntervalMilliseconds)
        {
            return false;
        }

        bool unavailableOverlay =
            snapshot.OverlayState ==
            BlueSageDiagnosticsOverlayStateV1.Unavailable;
        bool unavailableFreshness =
            snapshot.Freshness ==
            BlueSageDiagnosticsFreshnessV1.Unavailable;
        if (unavailableOverlay ||
            unavailableFreshness)
        {
            return unavailableOverlay &&
                   unavailableFreshness &&
                   snapshot.CapturedUtcTicks == 0 &&
                   snapshot.FreshUntilUtcTicks == 0;
        }

        if (snapshot.CapturedUtcTicks <= 0 ||
            snapshot.CapturedUtcTicks >
                nowUtcTicks +
                BlueSageExtensionAbiV1
                    .MaximumRequestFutureSkewTicks ||
            snapshot.FreshUntilUtcTicks <= 0 ||
            snapshot.FreshUntilUtcTicks >
                snapshot.CapturedUtcTicks +
                BlueSageExtensionAbiV1
                    .MaximumContributionLifetimeTicks)
        {
            return false;
        }

        return snapshot.Freshness switch
        {
            BlueSageDiagnosticsFreshnessV1.Fresh =>
                snapshot.FreshUntilUtcTicks >=
                nowUtcTicks,
            BlueSageDiagnosticsFreshnessV1.Stale =>
                snapshot.FreshUntilUtcTicks <
                nowUtcTicks,
            _ => false,
        };
    }

    private void ClearBadgeProvider()
    {
        _badgeProvider = null;
        _badgeOwnerGuid = string.Empty;
        _badgeOwnerVersion = null;
        _badgeOwnerAssembly = null;
        _badgeRegistrationGeneration = 0;
    }

    private void ClearDiagnosticsProvider()
    {
        _diagnosticsProvider = null;
        _diagnosticsOwnerGuid = string.Empty;
        _diagnosticsOwnerVersion = null;
        _diagnosticsOwnerAssembly = null;
        _diagnosticsRegistrationGeneration = 0;
    }

    private void ClearHostCapabilityProvider()
    {
        _hostCapabilityProvider = null;
        _hostCapabilityOwnerGuid = string.Empty;
        _hostCapabilityOwnerVersion = null;
        _hostCapabilityOwnerAssembly = null;
        _hostCapabilityRegistrationGeneration = 0;
    }
}
