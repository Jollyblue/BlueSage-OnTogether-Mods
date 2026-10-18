using System.Reflection;
using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BlueSageExtensionContractTests
{
    [Test]
    public void V1_contracts_are_public_typed_and_contain_only_primitive_or_enum_data()
    {
        Type[] contracts =
        {
            typeof(IBlueSageIdentityTruthV1),
            typeof(IBlueSageBadgeContributionV1),
            typeof(IBlueSageDiagnosticsProviderV1),
            typeof(BlueSageBadgeContributionRequestV1),
            typeof(BlueSageBadgeContributionResponseV1),
            typeof(BlueSageDiagnosticsSnapshotV1),
        };

        Assert.Multiple(() =>
        {
            Assert.That(contracts.All(type => type.IsPublic), Is.True);
            Assert.That(typeof(BlueSageBadgeContributionRequestV1).GetProperties()
                .Select(property => property.Name), Is.EqualTo(new[]
                {
                    "SteamId",
                    "PurrNetId",
                    "LobbyEpoch",
                    "EvidenceGeneration",
                    "Nonce",
                    "IssuedUtcTicks",
                }));
            Assert.That(typeof(BlueSageBadgeContributionResponseV1).GetProperties()
                .Select(property => property.Name), Is.EqualTo(new[]
                {
                    "ContributedBadgePresent",
                    "LobbyEpoch",
                    "EvidenceGeneration",
                    "Nonce",
                    "FreshUntilUtcTicks",
                }));
            Assert.That(contracts.SelectMany(type => type.GetProperties())
                .Any(property => property.PropertyType == typeof(string)), Is.False);
            Assert.That(contracts.SelectMany(type => type.GetProperties())
                .All(property => IsAllowed(property.PropertyType)), Is.True);
        });
    }

    [Test]
    public void Creator_response_cannot_return_identity_text_glyph_or_provider_metadata()
    {
        string[] names = typeof(BlueSageBadgeContributionResponseV1)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(member => member.Name)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Not.Contain("SteamId"));
            Assert.That(names, Does.Not.Contain("PurrNetId"));
            Assert.That(names.Any(name => name.Contains("Name", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(names.Any(name => name.Contains("Text", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(names.Any(name => name.Contains("Glyph", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(names.Any(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase)), Is.False);
            Assert.That(names.Any(name => name.Contains("Authority", StringComparison.OrdinalIgnoreCase)), Is.False);
        });
    }

    private static bool IsAllowed(Type type) =>
        type.IsPrimitive || type.IsEnum;
}
