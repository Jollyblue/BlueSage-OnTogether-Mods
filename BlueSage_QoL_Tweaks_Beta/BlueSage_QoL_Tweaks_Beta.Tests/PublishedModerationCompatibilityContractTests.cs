using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PublishedModerationCompatibilityContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Moderation_transport_remains_on_the_published_0_2_2_member_data_contract()
    {
        string policy = Read("src", "Core", "ModerationProtocolPolicy.cs");
        string capability = Read("src", "Core", "ModerationCapabilityPolicy.cs");

        Assert.Multiple(() =>
        {
            Assert.That(policy, Does.Contain(
                "MemberDataKey = \"bluesage_moderation_request_v2\""));
            Assert.That(policy, Does.Contain("MemberDataPrefix = \"v2|\""));
            Assert.That(capability, Does.Contain("Version = \"v3-memberdata\""));
        });
    }

    [Test]
    public void Missing_or_unknown_client_payloads_are_ignored_without_an_action()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ModerationProtocolPolicy.TryParseMemberData(string.Empty, out _),
                Is.False,
                "Vanilla and older clients that publish no request must remain inert.");
            Assert.That(
                ModerationProtocolPolicy.TryParseMemberData(
                    "v3|ban|76561198012345678|none|1700000000|ab12CD34",
                    out _),
                Is.False,
                "An unknown future transport must fail closed instead of being guessed.");
        });
    }

    [Test]
    public void Host_actions_target_verified_native_identity_without_requiring_target_mod_presence()
    {
        string controller = Read("src", "SteamIdModerationController.cs");
        string action = Slice(
            controller,
            "internal static bool TryExecuteHostAction(",
            "internal static string[] GetCurrentPlayerSteamIds()");

        Assert.Multiple(() =>
        {
            Assert.That(action, Does.Contain("TryGetOwnedHostLobby"));
            Assert.That(action, Does.Contain("ModerationAuthorizationPolicy.IsAuthorizedIssuer"));
            Assert.That(action, Does.Contain("TryBan("));
            Assert.That(action, Does.Contain("TryUnban("));
            Assert.That(action, Does.Not.Contain("CompatibleClientPresencePolicy"));
            Assert.That(action, Does.Not.Contain("PluginCompatibility.BlueSagePlayerLimitLiftGuid"));
            Assert.That(action, Does.Not.Contain("Metadata.Version"));
        });
    }

    [Test]
    public void QoL_and_PlayerLimitLift_remain_optional_peers_without_hard_package_coupling()
    {
        string lowLevelRoot = Directory.GetParent(Root)?.FullName
            ?? throw new DirectoryNotFoundException("LowLevel-RE root not found.");
        string qolManifest = Read("manifest.json");
        string playerLimitManifest = File.ReadAllText(Path.Combine(
            lowLevelRoot,
            "BlueSagePatched_PlayerLimitLift",
            "manifest.json"));

        Assert.Multiple(() =>
        {
            Assert.That(qolManifest, Does.Not.Contain("PlayerLimitLift"));
            Assert.That(playerLimitManifest, Does.Not.Contain("QoL_Tweaks"));
            Assert.That(qolManifest, Does.Contain("BepInEx-BepInExPack-5.4.2305"));
            Assert.That(playerLimitManifest, Does.Contain("BepInEx-BepInExPack-5.4.2305"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Slice(string value, string start, string end)
    {
        int startIndex = value.IndexOf(start, StringComparison.Ordinal);
        int endIndex = value.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), "Missing source start marker: " + start);
        Assert.That(endIndex, Is.GreaterThan(startIndex), "Missing source end marker: " + end);
        return value.Substring(startIndex, endIndex - startIndex);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException(
                "Could not find BlueSage QoL project root.");
    }
}
