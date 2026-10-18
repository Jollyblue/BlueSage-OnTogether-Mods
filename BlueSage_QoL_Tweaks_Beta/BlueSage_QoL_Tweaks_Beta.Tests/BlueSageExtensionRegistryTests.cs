using System.Reflection;
using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BlueSageExtensionRegistryTests
{
    private const string NeutralGuid = "example.neutral.badges";
    private static readonly Version NeutralVersion = new(1, 2, 3);

    [Test]
    public void Registration_requires_loaded_owner_assembly_guid_version_and_ABI_and_allows_one_provider()
    {
        var catalog = new FakeCatalog(
            NeutralGuid,
            typeof(FakeProvider).Assembly,
            NeutralVersion,
            loaded: true);
        var registry = new BlueSageExtensionRegistryCore(catalog);
        var provider = new FakeProvider();

        BlueSageExtensionRegistrationTokenV1 accepted = registry.RegisterBadgeContribution(
            provider,
            NeutralGuid,
            NeutralVersion,
            BlueSageExtensionAbiV1.Version);

        Assert.Multiple(() =>
        {
            Assert.That(accepted.IsValid, Is.True);
            Assert.That(registry.RegisterBadgeContribution(
                new FakeProvider(),
                NeutralGuid,
                NeutralVersion,
                BlueSageExtensionAbiV1.Version).IsValid, Is.False);
            Assert.That(new BlueSageExtensionRegistryCore(catalog).RegisterBadgeContribution(
                provider,
                "example.wrong",
                NeutralVersion,
                BlueSageExtensionAbiV1.Version).IsValid, Is.False);
            Assert.That(new BlueSageExtensionRegistryCore(catalog).RegisterBadgeContribution(
                provider,
                NeutralGuid,
                new Version(9, 9),
                BlueSageExtensionAbiV1.Version).IsValid, Is.False);
            Assert.That(new BlueSageExtensionRegistryCore(catalog).RegisterBadgeContribution(
                provider,
                NeutralGuid,
                NeutralVersion,
                999).IsValid, Is.False);
        });
    }

    [Test]
    public void Read_rechecks_loaded_owner_context_nonce_generation_and_freshness()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(NeutralGuid, typeof(FakeProvider).Assembly, NeutralVersion, true);
        var provider = new FakeProvider { ContributedBadgePresent = true, FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond };
        var registry = new BlueSageExtensionRegistryCore(catalog);
        registry.RegisterBadgeContribution(provider, NeutralGuid, NeutralVersion, BlueSageExtensionAbiV1.Version);
        registry.SetEvidenceContext(lobbyEpoch: 7, evidenceGeneration: 11);
        var request = Request(now, lobbyEpoch: 7, generation: 11, nonce: 13);

        Assert.That(registry.TryReadBadgeContribution(request, now, out BlueSageBadgeContributionResponseV1 response), Is.True);
        Assert.That(response.ContributedBadgePresent, Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryReadBadgeContribution(
                Request(now, lobbyEpoch: 8, generation: 11, nonce: 13), now, out _), Is.False);
            Assert.That(registry.TryReadBadgeContribution(
                Request(now, lobbyEpoch: 7, generation: 12, nonce: 13), now, out _), Is.False);
        });

        provider.ResponseNonceOffset = 1;
        Assert.That(registry.TryReadBadgeContribution(request, now, out _), Is.False);
        provider.ResponseNonceOffset = 0;
        provider.FreshUntilUtcTicks = now - 1;
        Assert.That(registry.TryReadBadgeContribution(request, now, out _), Is.False);
        provider.FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond;
        catalog.Loaded = false;
        Assert.That(registry.TryReadBadgeContribution(request, now, out _), Is.False);
    }

    [Test]
    public void Provider_exceptions_are_generic_unavailable_and_generation_token_controls_unregister()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(NeutralGuid, typeof(FakeProvider).Assembly, NeutralVersion, true);
        var provider = new FakeProvider
        {
            Throw = true,
            FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond,
        };
        var registry = new BlueSageExtensionRegistryCore(catalog);
        BlueSageExtensionRegistrationTokenV1 token = registry.RegisterBadgeContribution(
            provider,
            NeutralGuid,
            NeutralVersion,
            BlueSageExtensionAbiV1.Version);
        registry.SetEvidenceContext(7, 11);

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryReadBadgeContribution(Request(now, 7, 11, 13), now, out _), Is.False);
            Assert.That(registry.Unregister(new BlueSageExtensionRegistrationTokenV1(token.Generation + 1)), Is.False);
            Assert.That(registry.Unregister(token), Is.True);
            Assert.That(registry.Unregister(token), Is.False);
        });
    }

    [Test]
    public void Badge_only_context_allows_read_only_contribution_without_authorizing_host_capabilities()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(
            NeutralGuid,
            typeof(FakeProvider).Assembly,
            NeutralVersion,
            loaded: true);
        var provider = new FakeProvider
        {
            ContributedBadgePresent = true,
            FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond,
        };
        var registry = new BlueSageExtensionRegistryCore(catalog);
        registry.RegisterBadgeContribution(
            provider,
            NeutralGuid,
            NeutralVersion,
            BlueSageExtensionAbiV1.Version);

        registry.SetBadgeEvidenceContext(
            lobbyEpoch: 7,
            evidenceGeneration: 11);

        Assert.Multiple(() =>
        {
            Assert.That(
                registry.TryReadBadgeContribution(
                    Request(now, 7, 11, 13),
                    now,
                    out BlueSageBadgeContributionResponseV1 response),
                Is.True);
            Assert.That(response.ContributedBadgePresent, Is.True);
            Assert.That(
                registry.TryCreateHostCapabilityRequest(
                    isHost: true,
                    nonce: 17,
                    nowUtcTicks: now,
                    out _),
                Is.False,
                "A read-only badge context must never become host/action authority.");
        });

        registry.SetEvidenceContext(0, 0);
        Assert.That(
            registry.TryReadBadgeContribution(
                Request(now, 7, 11, 19),
                now,
                out _),
            Is.False,
            "Clearing lobby evidence must also clear the read-only badge context.");
    }

    [Test]
    public void Clear_removes_provider_and_evidence_context()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        var catalog = new FakeCatalog(NeutralGuid, typeof(FakeProvider).Assembly, NeutralVersion, true);
        var provider = new FakeProvider { FreshUntilUtcTicks = now + TimeSpan.TicksPerSecond };
        var registry = new BlueSageExtensionRegistryCore(catalog);
        registry.RegisterBadgeContribution(provider, NeutralGuid, NeutralVersion, BlueSageExtensionAbiV1.Version);
        registry.SetEvidenceContext(7, 11);
        registry.Clear();

        Assert.That(registry.TryReadBadgeContribution(Request(now, 7, 11, 13), now, out _), Is.False);
    }

    private static BlueSageBadgeContributionRequestV1 Request(
        long now,
        long lobbyEpoch,
        long generation,
        long nonce) => new(
            steamId: 100,
            purrNetId: 200,
            lobbyEpoch,
            evidenceGeneration: generation,
            nonce,
            issuedUtcTicks: now);

    private sealed class FakeProvider : IBlueSageBadgeContributionV1
    {
        internal bool ContributedBadgePresent;
        internal long FreshUntilUtcTicks;
        internal long ResponseNonceOffset;
        internal bool Throw;

        public BlueSageBadgeContributionResponseV1 GetBadgeContribution(
            BlueSageBadgeContributionRequestV1 request)
        {
            if (Throw) throw new InvalidOperationException("private detail");
            return new BlueSageBadgeContributionResponseV1(
                ContributedBadgePresent,
                request.LobbyEpoch,
                request.EvidenceGeneration,
                request.Nonce + ResponseNonceOffset,
                FreshUntilUtcTicks);
        }
    }

    private sealed class FakeCatalog : IBlueSageLoadedExtensionCatalogV1
    {
        private readonly string _guid;
        private readonly Assembly _assembly;
        private readonly Version _version;

        internal FakeCatalog(string guid, Assembly assembly, Version version, bool loaded)
        {
            _guid = guid;
            _assembly = assembly;
            _version = version;
            Loaded = loaded;
        }

        internal bool Loaded { get; set; }

        public bool TryGetLoaded(
            string pluginGuid,
            out BlueSageLoadedExtensionV1 extension)
        {
            if (Loaded && string.Equals(pluginGuid, _guid, StringComparison.Ordinal))
            {
                extension = new BlueSageLoadedExtensionV1(_assembly, _version, true);
                return true;
            }

            extension = default;
            return false;
        }
    }
}
