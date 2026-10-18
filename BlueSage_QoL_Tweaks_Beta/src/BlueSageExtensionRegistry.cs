using System;
using System.Reflection;
using BepInEx.Bootstrap;
using BlueSage.QoLTweaks.Contracts;
using PluginInfo = BepInEx.PluginInfo;

namespace BlueSage.QoLTweaks;

public static class BlueSageExtensionRegistry
{
    private static readonly object Sync = new object();
    private static readonly BlueSageExtensionRegistryCore Core =
        new BlueSageExtensionRegistryCore(
            new ChainloaderExtensionCatalog());

    public static BlueSageExtensionRegistrationTokenV1 RegisterBadgeContributionV1(
        IBlueSageBadgeContributionV1 provider,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion)
    {
        lock (Sync)
        {
            return Core.RegisterBadgeContribution(
                provider,
                ownerGuid,
                ownerVersion,
                abiVersion);
        }
    }

    public static bool UnregisterBadgeContributionV1(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        lock (Sync)
        {
            return Core.UnregisterBadgeContribution(
                token);
        }
    }

    public static BlueSageExtensionRegistrationTokenV1
        RegisterDiagnosticsProviderV1(
            IBlueSageDiagnosticsProviderV1 provider,
            string ownerGuid,
            Version ownerVersion,
            int abiVersion)
    {
        lock (Sync)
        {
            return Core.RegisterDiagnostics(
                provider,
                ownerGuid,
                ownerVersion,
                abiVersion);
        }
    }

    public static bool UnregisterDiagnosticsProviderV1(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        lock (Sync)
        {
            return Core.UnregisterDiagnostics(
                token);
        }
    }

    public static BlueSageExtensionRegistrationTokenV1
        RegisterHostCapabilityProviderV1(
            IBlueSageHostCapabilityProviderV1 provider,
            string ownerGuid,
            Version ownerVersion,
            int abiVersion)
    {
        lock (Sync)
        {
            return Core.RegisterHostCapability(
                provider,
                ownerGuid,
                ownerVersion,
                abiVersion);
        }
    }

    public static bool UnregisterHostCapabilityProviderV1(
        BlueSageExtensionRegistrationTokenV1 token)
    {
        lock (Sync)
        {
            return Core.UnregisterHostCapability(token);
        }
    }

    public static long RegisterDiagnosticsProviderDynamicV1(
        Func<long, long[]> provider,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion)
    {
        if (provider == null ||
            provider.Target != null ||
            !provider.Method.IsStatic ||
            provider.Method.DeclaringType == null)
        {
            return 0;
        }

        Assembly ownerAssembly =
            provider.Method.DeclaringType.Assembly;
        lock (Sync)
        {
            return Core.RegisterDiagnostics(
                new DynamicDiagnosticsProvider(provider),
                ownerGuid,
                ownerVersion,
                abiVersion,
                ownerAssembly).Generation;
        }
    }

    public static bool UnregisterDiagnosticsProviderDynamicV1(
        long generation)
    {
        lock (Sync)
        {
            return Core.UnregisterDiagnostics(
                new BlueSageExtensionRegistrationTokenV1(
                    generation));
        }
    }

    public static long RegisterDiagnosticsProviderDynamicV2(
        Func<long, long[]> provider,
        Func<bool, bool> overlayVisibilitySetter,
        string ownerGuid,
        Version ownerVersion,
        int abiVersion)
    {
        if (provider == null ||
            overlayVisibilitySetter == null ||
            provider.Target != null ||
            overlayVisibilitySetter.Target != null ||
            !provider.Method.IsStatic ||
            !overlayVisibilitySetter.Method.IsStatic ||
            provider.Method.DeclaringType == null ||
            overlayVisibilitySetter.Method.DeclaringType == null ||
            provider.Method.DeclaringType.Assembly !=
                overlayVisibilitySetter.Method.DeclaringType.Assembly)
        {
            return 0;
        }

        Assembly ownerAssembly =
            provider.Method.DeclaringType.Assembly;
        lock (Sync)
        {
            return Core.RegisterDiagnostics(
                new DynamicDiagnosticsProvider(
                    provider,
                    overlayVisibilitySetter),
                ownerGuid,
                ownerVersion,
                abiVersion,
                ownerAssembly).Generation;
        }
    }

    internal static void BeginLobby(
        long lobbyEpoch,
        long evidenceGeneration)
    {
        lock (Sync)
        {
            Core.SetEvidenceContext(
                lobbyEpoch,
                evidenceGeneration);
        }
    }

    internal static void BeginBadgePresentation(
        long lobbyEpoch,
        long evidenceGeneration)
    {
        lock (Sync)
        {
            Core.SetBadgeEvidenceContext(
                lobbyEpoch,
                evidenceGeneration);
        }
    }

    internal static bool TryReadBadgeContribution(
        BlueSageBadgeContributionRequestV1 request,
        long nowUtcTicks,
        out BlueSageBadgeContributionResponseV1 response)
    {
        lock (Sync)
        {
            return Core.TryReadBadgeContribution(
                request,
                nowUtcTicks,
                out response);
        }
    }

    internal static bool TryReadDiagnostics(
        long nowUtcTicks,
        out BlueSageDiagnosticsSnapshotV1 snapshot)
    {
        lock (Sync)
        {
            return Core.TryReadDiagnostics(
                nowUtcTicks,
                out snapshot);
        }
    }

    internal static bool TrySetDiagnosticsOverlayVisible(
        bool visible)
    {
        lock (Sync)
        {
            return Core.TrySetDiagnosticsOverlayVisible(visible);
        }
    }

    internal static bool TryReadHostCapability(
        bool isHost,
        long nonce,
        long nowUtcTicks,
        out BlueSageHostCapabilitySnapshotV1 snapshot)
    {
        lock (Sync)
        {
            snapshot = default;
            return Core.TryCreateHostCapabilityRequest(
                       isHost, nonce, nowUtcTicks, out BlueSageHostCapabilityRequestV1 request) &&
                   Core.TryReadHostCapability(request, nowUtcTicks, out snapshot);
        }
    }

    internal static bool TryExecuteHostCapability(
        BlueSageHostCapabilityActionV1 action,
        bool isHost,
        long nonce,
        long nowUtcTicks,
        out BlueSageHostCapabilityReceiptV1 receipt)
    {
        lock (Sync)
        {
            receipt = default;
            return Core.TryCreateHostCapabilityRequest(
                       isHost, nonce, nowUtcTicks, out BlueSageHostCapabilityRequestV1 request) &&
                   Core.TryExecuteHostCapability(action, request, nowUtcTicks, out receipt);
        }
    }

    internal static bool HasLiveHostCapabilityProvider()
    {
        lock (Sync)
        {
            return Core.HasLiveHostCapabilityProvider();
        }
    }

    internal static void ClearLobbyState()
    {
        lock (Sync)
        {
            Core.SetEvidenceContext(0, 0);
            Core.SetBadgeEvidenceContext(0, 0);
        }
    }

    internal static void ClearAll()
    {
        lock (Sync)
        {
            Core.Clear();
        }
    }

    private sealed class ChainloaderExtensionCatalog :
        IBlueSageLoadedExtensionCatalogV1
    {
        public bool TryGetLoaded(
            string pluginGuid,
            out BlueSageLoadedExtensionV1 extension)
        {
            extension = default;
            if (string.IsNullOrWhiteSpace(pluginGuid) ||
                !Chainloader.PluginInfos.TryGetValue(
                    pluginGuid,
                    out PluginInfo info) ||
                info?.Instance == null ||
                info.Metadata?.Version == null)
            {
                return false;
            }

            Assembly ownerAssembly =
                info.Instance.GetType().Assembly;
            extension = new BlueSageLoadedExtensionV1(
                ownerAssembly,
                info.Metadata.Version,
                loaded: true);
            return true;
        }
    }

    private sealed class DynamicDiagnosticsProvider :
        IBlueSageDiagnosticsProviderV1,
        IBlueSageDiagnosticsControlV2
    {
        private const int PayloadLength = 5;
        private readonly Func<long, long[]> _provider;
        private readonly Func<bool, bool> _overlayVisibilitySetter;

        internal DynamicDiagnosticsProvider(
            Func<long, long[]> provider,
            Func<bool, bool> overlayVisibilitySetter = null)
        {
            _provider = provider ??
                throw new ArgumentNullException(nameof(provider));
            _overlayVisibilitySetter = overlayVisibilitySetter;
        }

        public BlueSageDiagnosticsSnapshotV1 GetSnapshot(
            long nowUtcTicks)
        {
            long[] payload = _provider(nowUtcTicks);
            if (payload == null ||
                payload.Length != PayloadLength ||
                payload[0] < 0 ||
                payload[0] > 2 ||
                payload[1] < 0 ||
                payload[1] > 2 ||
                payload[2] <
                    BlueSageDiagnosticsPolicyV1
                        .MinimumIntervalMilliseconds ||
                payload[2] >
                    BlueSageDiagnosticsPolicyV1
                        .MaximumIntervalMilliseconds)
            {
                throw new InvalidOperationException(
                    "Diagnostics payload failed the public ABI bounds.");
            }

            return new BlueSageDiagnosticsSnapshotV1(
                (BlueSageDiagnosticsOverlayStateV1)payload[0],
                (BlueSageDiagnosticsFreshnessV1)payload[1],
                (int)payload[2],
                payload[3],
                payload[4]);
        }

        public bool TrySetOverlayVisible(bool visible)
        {
            return _overlayVisibilitySetter != null &&
                   _overlayVisibilitySetter(visible);
        }
    }
}
