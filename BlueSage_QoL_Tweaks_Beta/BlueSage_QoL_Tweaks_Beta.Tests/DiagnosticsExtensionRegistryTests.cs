using System.Reflection;
using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class DiagnosticsExtensionRegistryTests
{
    private const string DiagnosticsGuid =
        "example.neutral.diagnostics";
    private const string BadgeGuid =
        "example.neutral.badges";
    private static readonly Version ProviderVersion =
        new(1, 2, 3);

    [Test]
    public void Registration_requires_the_exact_loaded_owner_and_allows_one_diagnostics_provider()
    {
        FakeCatalog catalog = new();
        catalog.Set(
            DiagnosticsGuid,
            typeof(FakeDiagnosticsProvider).Assembly,
            ProviderVersion,
            loaded: true);
        BlueSageExtensionRegistryCore registry = new(catalog);
        FakeDiagnosticsProvider provider = new();

        BlueSageExtensionRegistrationTokenV1 accepted =
            registry.RegisterDiagnostics(
                provider,
                DiagnosticsGuid,
                ProviderVersion,
                BlueSageExtensionAbiV1.Version);

        Assert.Multiple(() =>
        {
            Assert.That(accepted.IsValid, Is.True);
            Assert.That(
                registry.RegisterDiagnostics(
                    new FakeDiagnosticsProvider(),
                    DiagnosticsGuid,
                    ProviderVersion,
                    BlueSageExtensionAbiV1.Version).IsValid,
                Is.False);
            Assert.That(
                new BlueSageExtensionRegistryCore(catalog)
                    .RegisterDiagnostics(
                        provider,
                        "example.wrong",
                        ProviderVersion,
                        BlueSageExtensionAbiV1.Version)
                    .IsValid,
                Is.False);
            Assert.That(
                new BlueSageExtensionRegistryCore(catalog)
                    .RegisterDiagnostics(
                        provider,
                        DiagnosticsGuid,
                        new Version(9, 9),
                        BlueSageExtensionAbiV1.Version)
                    .IsValid,
                Is.False);
            Assert.That(
                new BlueSageExtensionRegistryCore(catalog)
                    .RegisterDiagnostics(
                        provider,
                        DiagnosticsGuid,
                        ProviderVersion,
                        abiVersion: 999)
                    .IsValid,
                Is.False);
        });
    }

    [Test]
    public void Read_rechecks_loaded_owner_and_accepts_only_bounded_generic_snapshots()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        FakeCatalog catalog = new();
        catalog.Set(
            DiagnosticsGuid,
            typeof(FakeDiagnosticsProvider).Assembly,
            ProviderVersion,
            loaded: true);
        FakeDiagnosticsProvider provider = new()
        {
            Snapshot = FreshSnapshot(now),
        };
        BlueSageExtensionRegistryCore registry = new(catalog);
        registry.RegisterDiagnostics(
            provider,
            DiagnosticsGuid,
            ProviderVersion,
            BlueSageExtensionAbiV1.Version);

        Assert.That(
            registry.TryReadDiagnostics(
                now,
                out BlueSageDiagnosticsSnapshotV1 snapshot),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(
                snapshot.OverlayState,
                Is.EqualTo(
                    BlueSageDiagnosticsOverlayStateV1.Visible));
            Assert.That(
                snapshot.Freshness,
                Is.EqualTo(
                    BlueSageDiagnosticsFreshnessV1.Fresh));
            Assert.That(
                snapshot.IntervalMilliseconds,
                Is.EqualTo(250));
        });

        provider.Snapshot = new BlueSageDiagnosticsSnapshotV1(
            BlueSageDiagnosticsOverlayStateV1.Visible,
            BlueSageDiagnosticsFreshnessV1.Fresh,
            250,
            capturedUtcTicks: now,
            freshUntilUtcTicks:
                now +
                BlueSageExtensionAbiV1
                    .MaximumContributionLifetimeTicks +
                1);
        Assert.That(
            registry.TryReadDiagnostics(now, out _),
            Is.False,
            "A provider cannot extend freshness beyond the public ABI lifetime.");

        provider.Snapshot = FreshSnapshot(now);
        catalog.Set(
            DiagnosticsGuid,
            typeof(FakeDiagnosticsProvider).Assembly,
            ProviderVersion,
            loaded: false);
        Assert.That(
            registry.TryReadDiagnostics(now, out _),
            Is.False,
            "Provider availability is rechecked against the live plugin catalog.");
    }

    [Test]
    public void Explicit_unavailable_snapshot_is_valid_but_mixed_or_exception_state_fails_closed()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        FakeCatalog catalog = new();
        catalog.Set(
            DiagnosticsGuid,
            typeof(FakeDiagnosticsProvider).Assembly,
            ProviderVersion,
            loaded: true);
        FakeDiagnosticsProvider provider = new()
        {
            Snapshot = new BlueSageDiagnosticsSnapshotV1(
                BlueSageDiagnosticsOverlayStateV1.Unavailable,
                BlueSageDiagnosticsFreshnessV1.Unavailable,
                250,
                capturedUtcTicks: 0,
                freshUntilUtcTicks: 0),
        };
        BlueSageExtensionRegistryCore registry = new(catalog);
        registry.RegisterDiagnostics(
            provider,
            DiagnosticsGuid,
            ProviderVersion,
            BlueSageExtensionAbiV1.Version);

        Assert.That(
            registry.TryReadDiagnostics(now, out _),
            Is.True);

        provider.Snapshot = new BlueSageDiagnosticsSnapshotV1(
            BlueSageDiagnosticsOverlayStateV1.Visible,
            BlueSageDiagnosticsFreshnessV1.Unavailable,
            250,
            capturedUtcTicks: now,
            freshUntilUtcTicks: 0);
        Assert.That(
            registry.TryReadDiagnostics(now, out _),
            Is.False);

        provider.Throw = true;
        Assert.That(
            registry.TryReadDiagnostics(now, out _),
            Is.False);
    }

    [Test]
    public void Badge_and_diagnostics_registration_tokens_unregister_independently()
    {
        FakeCatalog catalog = new();
        Assembly assembly = typeof(FakeDiagnosticsProvider).Assembly;
        catalog.Set(
            DiagnosticsGuid,
            assembly,
            ProviderVersion,
            loaded: true);
        catalog.Set(
            BadgeGuid,
            assembly,
            ProviderVersion,
            loaded: true);
        BlueSageExtensionRegistryCore registry = new(catalog);

        BlueSageExtensionRegistrationTokenV1 diagnosticsToken =
            registry.RegisterDiagnostics(
                new FakeDiagnosticsProvider(),
                DiagnosticsGuid,
                ProviderVersion,
                BlueSageExtensionAbiV1.Version);
        BlueSageExtensionRegistrationTokenV1 badgeToken =
            registry.RegisterBadgeContribution(
                new FakeBadgeProvider(),
                BadgeGuid,
                ProviderVersion,
                BlueSageExtensionAbiV1.Version);

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnosticsToken.Generation,
                Is.Not.EqualTo(badgeToken.Generation));
            Assert.That(
                registry.UnregisterDiagnostics(
                    badgeToken),
                Is.False);
            Assert.That(
                registry.UnregisterDiagnostics(
                    diagnosticsToken),
                Is.True);
            Assert.That(
                registry.Unregister(badgeToken),
                Is.True);
        });
    }

    [Test]
    public void Diagnostics_can_bind_a_primitive_adapter_to_the_exact_loaded_owner_assembly()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        FakeCatalog catalog = new();
        Assembly ownerAssembly = typeof(ExternalOwnerMarker).Assembly;
        catalog.Set(
            DiagnosticsGuid,
            ownerAssembly,
            ProviderVersion,
            loaded: true);
        BlueSageExtensionRegistryCore registry = new(catalog);

        BlueSageExtensionRegistrationTokenV1 token =
            registry.RegisterDiagnostics(
                new FakeDiagnosticsProvider
                {
                    Snapshot = FreshSnapshot(now),
                },
                DiagnosticsGuid,
                ProviderVersion,
                BlueSageExtensionAbiV1.Version,
                ownerAssembly);

        Assert.Multiple(() =>
        {
            Assert.That(token.IsValid, Is.True);
            Assert.That(
                registry.TryReadDiagnostics(now, out _),
                Is.True);
        });
    }

    [Test]
    public void Diagnostics_reject_an_explicit_owner_assembly_that_is_not_the_loaded_plugin()
    {
        FakeCatalog catalog = new();
        catalog.Set(
            DiagnosticsGuid,
            typeof(ExternalOwnerMarker).Assembly,
            ProviderVersion,
            loaded: true);
        BlueSageExtensionRegistryCore registry = new(catalog);

        BlueSageExtensionRegistrationTokenV1 token =
            registry.RegisterDiagnostics(
                new FakeDiagnosticsProvider(),
                DiagnosticsGuid,
                ProviderVersion,
                BlueSageExtensionAbiV1.Version,
                typeof(string).Assembly);

        Assert.That(token.IsValid, Is.False);
    }

    [Test]
    public void Diagnostics_control_changes_only_the_exact_loaded_provider_and_fails_closed_after_unload()
    {
        FakeCatalog catalog = new();
        catalog.Set(
            DiagnosticsGuid,
            typeof(FakeDiagnosticsProvider).Assembly,
            ProviderVersion,
            loaded: true);
        FakeDiagnosticsProvider provider = new();
        BlueSageExtensionRegistryCore registry = new(catalog);
        registry.RegisterDiagnostics(
            provider,
            DiagnosticsGuid,
            ProviderVersion,
            BlueSageExtensionAbiV1.Version);

        Assert.Multiple(() =>
        {
            Assert.That(
                registry.TrySetDiagnosticsOverlayVisible(false),
                Is.True);
            Assert.That(provider.OverlayVisible, Is.False);
        });

        catalog.Set(
            DiagnosticsGuid,
            typeof(FakeDiagnosticsProvider).Assembly,
            ProviderVersion,
            loaded: false);
        Assert.That(
            registry.TrySetDiagnosticsOverlayVisible(true),
            Is.False);
        Assert.That(provider.OverlayVisible, Is.False);
    }

    private static BlueSageDiagnosticsSnapshotV1 FreshSnapshot(
        long now) =>
        new(
            BlueSageDiagnosticsOverlayStateV1.Visible,
            BlueSageDiagnosticsFreshnessV1.Fresh,
            intervalMilliseconds: 250,
            capturedUtcTicks: now,
            freshUntilUtcTicks: now + TimeSpan.TicksPerSecond);

    private sealed class FakeDiagnosticsProvider :
        IBlueSageDiagnosticsProviderV1,
        IBlueSageDiagnosticsControlV2
    {
        internal BlueSageDiagnosticsSnapshotV1 Snapshot;
        internal bool Throw;
        internal bool OverlayVisible = true;

        public BlueSageDiagnosticsSnapshotV1 GetSnapshot(
            long nowUtcTicks)
        {
            if (Throw)
            {
                throw new InvalidOperationException(
                    "private detail");
            }

            return Snapshot;
        }

        public bool TrySetOverlayVisible(bool visible)
        {
            OverlayVisible = visible;
            return true;
        }
    }

    private sealed class FakeBadgeProvider :
        IBlueSageBadgeContributionV1
    {
        public BlueSageBadgeContributionResponseV1
            GetBadgeContribution(
                BlueSageBadgeContributionRequestV1 request) =>
            new(
                contributedBadgePresent: false,
                request.LobbyEpoch,
                request.EvidenceGeneration,
                request.Nonce,
                request.IssuedUtcTicks +
                TimeSpan.TicksPerSecond);
    }

    private sealed class ExternalOwnerMarker
    {
    }

    private sealed class FakeCatalog :
        IBlueSageLoadedExtensionCatalogV1
    {
        private readonly Dictionary<
            string,
            BlueSageLoadedExtensionV1> _entries =
            new(StringComparer.Ordinal);

        internal void Set(
            string guid,
            Assembly assembly,
            Version version,
            bool loaded)
        {
            _entries[guid] =
                new BlueSageLoadedExtensionV1(
                    assembly,
                    version,
                    loaded);
        }

        public bool TryGetLoaded(
            string pluginGuid,
            out BlueSageLoadedExtensionV1 extension)
        {
            return _entries.TryGetValue(
                pluginGuid,
                out extension);
        }
    }
}
