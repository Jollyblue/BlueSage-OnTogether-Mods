using System.Reflection;
using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class HostCapabilityExtensionRegistryTests
{
    private const string OwnerGuid = "example.host.capability";
    private static readonly Version OwnerVersion = new(1, 2, 3);

    [Test]
    public void Fresh_exact_context_snapshot_is_accepted_and_generation_unregisters_it()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(typeof(FakeProvider).Assembly);
        var provider = new FakeProvider(now);
        var registry = new BlueSageExtensionRegistryCore(catalog);
        registry.SetEvidenceContext(7, 11);

        BlueSageExtensionRegistrationTokenV1 token = registry.RegisterHostCapability(
            provider, OwnerGuid, OwnerVersion, BlueSageExtensionAbiV1.Version);
        var request = Request(now);

        Assert.Multiple(() =>
        {
            Assert.That(token.IsValid, Is.True);
            Assert.That(registry.TryReadHostCapability(request, now, out BlueSageHostCapabilitySnapshotV1 snapshot), Is.True);
            Assert.That(snapshot.State, Is.EqualTo(BlueSageHostCapabilityStateV1.Ready));
            Assert.That(snapshot.Title, Is.EqualTo("Companion"));
            Assert.That(registry.UnregisterHostCapability(new(token.Generation + 1)), Is.False);
            Assert.That(registry.UnregisterHostCapability(token), Is.True);
            Assert.That(registry.TryReadHostCapability(request, now, out _), Is.False);
        });
    }

    [Test]
    public void Stale_mismatched_unloaded_and_throwing_providers_fail_closed()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(typeof(FakeProvider).Assembly);
        var provider = new FakeProvider(now);
        var registry = new BlueSageExtensionRegistryCore(catalog);
        registry.SetEvidenceContext(7, 11);
        registry.RegisterHostCapability(provider, OwnerGuid, OwnerVersion, BlueSageExtensionAbiV1.Version);

        provider.NonceOffset = 1;
        Assert.That(registry.TryReadHostCapability(Request(now), now, out _), Is.False);
        provider.NonceOffset = 0;
        provider.FreshUntilUtcTicks = now - 1;
        Assert.That(registry.TryReadHostCapability(Request(now), now, out _), Is.False);
        provider.FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond;
        provider.Throw = true;
        Assert.That(registry.TryReadHostCapability(Request(now), now, out _), Is.False);
        provider.Throw = false;
        catalog.Loaded = false;
        Assert.That(registry.TryReadHostCapability(Request(now), now, out _), Is.False);
    }

    [Test]
    public void Actions_require_host_fresh_exact_context_and_matching_receipt()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(typeof(FakeProvider).Assembly);
        var provider = new FakeProvider(now);
        var registry = new BlueSageExtensionRegistryCore(catalog);
        registry.SetEvidenceContext(7, 11);
        registry.RegisterHostCapability(provider, OwnerGuid, OwnerVersion, BlueSageExtensionAbiV1.Version);

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryExecuteHostCapability(
                BlueSageHostCapabilityActionV1.RefreshSnapshot,
                Request(now, isHost: true), now, out BlueSageHostCapabilityReceiptV1 receipt), Is.True);
            Assert.That(receipt.Succeeded, Is.True);
            Assert.That(registry.TryExecuteHostCapability(
                BlueSageHostCapabilityActionV1.RefreshSnapshot,
                Request(now, isHost: false), now, out _), Is.False);
        });

        provider.ReceiptNonceOffset = 1;
        Assert.That(registry.TryExecuteHostCapability(
            BlueSageHostCapabilityActionV1.RefreshSnapshot,
            Request(now, isHost: true), now, out _), Is.False);
    }

    private static BlueSageHostCapabilityRequestV1 Request(long now, bool isHost = true) =>
        new(7, 11, isHost, 13, now);

    private sealed class FakeProvider : IBlueSageHostCapabilityProviderV1
    {
        internal FakeProvider(long now) => FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond;
        internal long FreshUntilUtcTicks;
        internal long NonceOffset;
        internal long ReceiptNonceOffset;
        internal bool Throw;

        public BlueSageHostCapabilitySnapshotV1 GetSnapshot(BlueSageHostCapabilityRequestV1 request)
        {
            if (Throw) throw new InvalidOperationException("private detail");
            return new(
                BlueSageHostCapabilityStateV1.Ready,
                "Companion",
                "Private host tools ready.",
                request.LobbyEpoch,
                request.EvidenceGeneration,
                request.Nonce + NonceOffset,
                request.IssuedUtcTicks,
                FreshUntilUtcTicks,
                BlueSageHostCapabilityActionMaskV1.RefreshSnapshot);
        }

        public BlueSageHostCapabilityReceiptV1 TryExecute(
            BlueSageHostCapabilityActionV1 action,
            BlueSageHostCapabilityRequestV1 request)
        {
            if (Throw) throw new InvalidOperationException("private detail");
            return new(true, "Snapshot refreshed.", request.LobbyEpoch,
                request.EvidenceGeneration, request.Nonce + ReceiptNonceOffset,
                request.IssuedUtcTicks);
        }
    }

    private sealed class FakeCatalog : IBlueSageLoadedExtensionCatalogV1
    {
        private readonly Assembly _assembly;
        internal FakeCatalog(Assembly assembly) => _assembly = assembly;
        internal bool Loaded { get; set; } = true;

        public bool TryGetLoaded(string pluginGuid, out BlueSageLoadedExtensionV1 extension)
        {
            if (Loaded && pluginGuid == OwnerGuid)
            {
                extension = new(_assembly, OwnerVersion, true);
                return true;
            }
            extension = default;
            return false;
        }
    }
}
