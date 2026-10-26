using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BlueSage.QoLTweaks.Contracts;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 5e stage P (per-row host authority). The extension registry's host-capability and badge-contribution
// context is keyed to the companion generation whenever the companion context is valid. The companion generation
// only counts rows that are in the Steam lobby, so a row outside the lobby no longer blanks the host's F6 label or
// the badge read. The authority generation stays internal (pruning, Clone Shield, absence checks).
// The first two fixtures are behavioural (pure policy + the real registry core); the last one pins the static,
// Steam-bound PlayerBadgeController wiring by source text because it cannot run outside the game.
public sealed class PerRowHostAuthorityTests
{
    private const string OwnerGuid = "example.per-row.host";
    private static readonly Version OwnerVersion = new(1, 0, 0);
    private const long Epoch = 7;
    private const long Companion = 5;
    private const long Authority = 9;

    [Test]
    public void The_shipped_default_is_per_row()
    {
        Assert.That(HostAuthorityRegistryKeyPolicy.LockedPerRowHostAuthority, Is.False);
    }

    [TestCase(true, Companion, false, 0L, ExpectedResult = Companion, TestName = "Unlocked: incomplete authority, valid companion keys the companion generation")]
    [TestCase(true, Companion, true, Authority, ExpectedResult = Companion, TestName = "Unlocked: both valid still keys the companion generation")]
    [TestCase(false, Companion, true, Authority, ExpectedResult = 0L, TestName = "Unlocked: an invalid companion context publishes no key")]
    [TestCase(true, 0L, true, Authority, ExpectedResult = 0L, TestName = "Unlocked: a zero companion generation publishes no key")]
    public long Unlocked_registry_key(bool companionValid, long companion, bool authorityValid, long authority) =>
        HostAuthorityRegistryKeyPolicy.RegistryGeneration(false, companionValid, companion, authorityValid, authority);

    [TestCase(true, Companion, true, Authority, ExpectedResult = Authority, TestName = "Locked: complete authority keys the authority generation (0.2.6)")]
    [TestCase(true, Companion, false, Authority, ExpectedResult = 0L, TestName = "Locked: incomplete authority clears the key even with a valid companion (0.2.6)")]
    [TestCase(false, 0L, true, Authority, ExpectedResult = Authority, TestName = "Locked: the companion context is ignored")]
    public long Locked_registry_key(bool companionValid, long companion, bool authorityValid, long authority) =>
        HostAuthorityRegistryKeyPolicy.RegistryGeneration(true, companionValid, companion, authorityValid, authority);

    [Test]
    public void F6_freshness_compares_against_the_generation_the_registry_is_keyed_to()
    {
        Assert.Multiple(() =>
        {
            Assert.That(HostAuthorityRegistryKeyPolicy.FreshnessGeneration(false, Companion, Authority), Is.EqualTo(Companion));
            Assert.That(HostAuthorityRegistryKeyPolicy.FreshnessGeneration(true, Companion, Authority), Is.EqualTo(Authority));
        });
    }

    [Test]
    public void Host_aligned_with_two_foreign_rows_the_F6_badge_read_answers_and_host_capability_is_available()
    {
        // The two foreign rows make the lobby-wide authority coverage incomplete (authority invalid). They are not
        // Steam lobby members, so they are not in the companion fingerprint and the companion context stays valid.
        long now = 10_000 * TimeSpan.TicksPerSecond;
        BlueSageExtensionRegistryCore registry = NewRegistry(now);
        long key = HostAuthorityRegistryKeyPolicy.RegistryGeneration(false, true, Companion, false, 0L);
        registry.SetEvidenceContext(Epoch, key);

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryReadBadgeContribution(TruthRequest(now, Companion), now, out BlueSageBadgeContributionResponseV1 response), Is.True,
                "The F6 read is stamped with the companion generation and must answer.");
            Assert.That(response.ContributedBadgePresent, Is.True);
            Assert.That(registry.TryCreateHostCapabilityRequest(true, 17, now, out BlueSageHostCapabilityRequestV1 request), Is.True);
            Assert.That(request.EvidenceGeneration, Is.EqualTo(Companion));
        });
    }

    [Test]
    public void Locked_branch_keeps_the_0_2_6_refusal_for_the_same_lobby()
    {
        long now = 10_000 * TimeSpan.TicksPerSecond;
        BlueSageExtensionRegistryCore registry = NewRegistry(now);
        long key = HostAuthorityRegistryKeyPolicy.RegistryGeneration(true, true, Companion, false, 0L);
        registry.SetEvidenceContext(Epoch, key);

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryReadBadgeContribution(TruthRequest(now, Companion), now, out _), Is.False);
            Assert.That(registry.TryCreateHostCapabilityRequest(true, 17, now, out _), Is.False);
        });
    }

    [Test]
    public void Unlocked_with_complete_authority_a_read_stamped_with_the_authority_generation_is_refused()
    {
        // Authority and companion counters move independently; only the keyed (companion) generation answers.
        long now = 10_000 * TimeSpan.TicksPerSecond;
        BlueSageExtensionRegistryCore registry = NewRegistry(now);
        registry.SetEvidenceContext(Epoch, HostAuthorityRegistryKeyPolicy.RegistryGeneration(false, true, Companion, true, Authority));

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryReadBadgeContribution(TruthRequest(now, Companion), now, out _), Is.True);
            Assert.That(registry.TryReadBadgeContribution(TruthRequest(now, Authority), now, out _), Is.False);
        });
    }

    [Test]
    public void PlayerBadgeController_has_one_registry_key_writer_and_every_consumer_reads_it()
    {
        string source = ReadSource("src", "Patches", "PlayerBadgeController.cs");
        string publish = Body(source, "private static void PublishRegistryKey()");
        string observeAuthority = Body(source, "private static bool ObserveEvidenceGeneration(");
        string readOnly = Body(source, "private static void BeginReadOnlyBadgePresentationContext()");
        string creator = Body(source, "private static bool TryReadCreatorContribution(");
        string withoutRoster = Body(source, "private static void RefreshPresentationsWithoutCompleteRoster()");

        Assert.Multiple(() =>
        {
            Assert.That(Regex.Matches(source, @"BlueSageExtensionRegistry\.BeginLobby\(").Count, Is.EqualTo(1), "One writer of the host/badge key.");
            Assert.That(publish, Does.Contain("BlueSageExtensionRegistry.BeginLobby("));
            Assert.That(publish, Does.Contain("HostAuthorityRegistryKeyPolicy.RegistryGeneration("));
            Assert.That(publish, Does.Contain("HostAuthorityRegistryKeyPolicy.LockedPerRowHostAuthority"));
            Assert.That(observeAuthority, Does.Not.Contain("BlueSageExtensionRegistry.ClearLobbyState()"), "An authority failure no longer clears a valid companion key directly.");
            Assert.That(observeAuthority, Does.Contain("PublishRegistryKey();"));
            Assert.That(Body(source, "private static bool ObserveCompanionEvidenceGeneration("), Does.Contain("PublishRegistryKey();"));
            Assert.That(readOnly, Does.Contain("if (_registryKeyGeneration > 0)"), "The read-only badge context never overrides a published key.");
            Assert.That(creator, Does.Contain("_registryKeyGeneration > 0"));
            Assert.That(creator, Does.Not.Contain("_evidenceContextValid"));
            Assert.That(withoutRoster, Does.Contain("ObserveCompanionContext(_lobbyKey);"), "The companion context refreshes on the incomplete path too.");
            Assert.That(withoutRoster, Does.Not.Contain("PruneRowsNotIn"), "An incomplete roster still never prunes.");
            Assert.That(source, Does.Contain("InvalidateCompanionEvidenceGeneration();\n            InvalidateEvidenceGeneration();\n            HideAllPresentations();"), "A failed poll clears the key.");
            Assert.That(source, Does.Contain("generationCurrent: truth.EvidenceGeneration ==\n                HostAuthorityRegistryKeyPolicy.FreshnessGeneration("));
        });
    }

    [Test]
    public void Foreign_rows_cannot_move_the_companion_generation_because_they_are_not_fingerprint_inputs()
    {
        // The companion fingerprint is built from the lobby, its Steam members, published helpers and the Verified
        // tuples only. A row outside the Steam lobby is never Verified and no native row count is an input.
        string source = ReadSource("src", "PlayerIdentityEvidenceController.cs");
        int start = source.IndexOf("CompanionExactIdentityFingerprintPolicy.TryBuildFingerprint(", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThan(0));
        string call = source.Substring(start, source.IndexOf("out fingerprintError);", start, StringComparison.Ordinal) - start);
        Assert.Multiple(() =>
        {
            Assert.That(call, Does.Contain("snapshot.LobbyMemberSteamIds"));
            Assert.That(call, Does.Contain("snapshot.Verified.Select(candidate => candidate.SteamId)"));
            Assert.That(call, Does.Contain("snapshot.Verified.Select(candidate => candidate.TransformInstanceId)"));
            Assert.That(call, Does.Not.Contain("NativeRows").And.Not.Contain("RawNative").And.Not.Contain("PlayerItemControllers"));
        });
    }

    [Test]
    public void The_refusal_line_names_the_registry_key()
    {
        string controller = ReadSource("src", "Patches", "PlayerRoleLabelController.cs");
        Assert.That(controller, Does.Contain("\"; registry keyed to \" + PlayerBadgeController.RegistryKeyForDiagnostics"));
    }

    private static BlueSageExtensionRegistryCore NewRegistry(long now)
    {
        var registry = new BlueSageExtensionRegistryCore(new FakeCatalog());
        registry.RegisterBadgeContribution(new FakeBadgeProvider(now), OwnerGuid, OwnerVersion, BlueSageExtensionAbiV1.Version);
        return registry;
    }

    private static BlueSageBadgeContributionRequestV1 TruthRequest(long now, long generation) =>
        new(steamId: 100, purrNetId: 200, lobbyEpoch: Epoch, evidenceGeneration: generation, nonce: 13, issuedUtcTicks: now);

    private static string ReadSource(params string[] parts)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string[] all = new string[parts.Length + 1];
        all[0] = root;
        Array.Copy(parts, 0, all, 1, parts.Length);
        return File.ReadAllText(Path.Combine(all)).Replace("\r\n", "\n");
    }

    private static string Body(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing: " + signature);
        int end = source.IndexOf("\n    }\n", start, StringComparison.Ordinal);
        return source.Substring(start, end - start);
    }

    private sealed class FakeBadgeProvider : IBlueSageBadgeContributionV1
    {
        private readonly long _now;
        internal FakeBadgeProvider(long now) => _now = now;

        public BlueSageBadgeContributionResponseV1 GetBadgeContribution(BlueSageBadgeContributionRequestV1 request) =>
            new(true, request.LobbyEpoch, request.EvidenceGeneration, request.Nonce, _now + TimeSpan.TicksPerSecond);
    }

    private sealed class FakeCatalog : IBlueSageLoadedExtensionCatalogV1
    {
        public bool TryGetLoaded(string pluginGuid, out BlueSageLoadedExtensionV1 extension)
        {
            if (string.Equals(pluginGuid, OwnerGuid, StringComparison.Ordinal))
            {
                extension = new BlueSageLoadedExtensionV1(typeof(FakeBadgeProvider).Assembly, OwnerVersion, true);
                return true;
            }

            extension = default;
            return false;
        }
    }
}
