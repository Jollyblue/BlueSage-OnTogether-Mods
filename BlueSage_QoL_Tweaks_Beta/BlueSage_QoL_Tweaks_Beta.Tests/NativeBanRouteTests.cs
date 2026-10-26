using System.IO;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage C, review P1 (ponytail-review of 2911bf9, 2026-10-19): the game's three Ban buttons (ID card
// TemporaryPhoto.ButtonBan, player row PlayerItemController.ButtonBan, report confirm PlayerPanelController.ButtonBan;
// decompiled build 25966942) all ban by PlayerSteamIDs.IndexOf(steamId). After a reconnect that is the stale row, so
// the live player stays. For the host each one now goes through the same host-verified path as /sidban
// (SteamIdModerationController.TryExecuteHostAction: live rows from the PurrNet table, RECONNECTED / LOADING labels,
// fail closed). Non-hosts are never touched: vanilla disables those buttons for them already.
[TestFixture]
public sealed class NativeBanRouteTests
{
    private const string Target = "76561198000000002";

    [TestCase(false, Target, NativeBanRoute.Vanilla, TestName = "Non_host_click_is_left_to_vanilla")]
    [TestCase(false, "", NativeBanRoute.Vanilla, TestName = "Non_host_click_without_an_id_is_left_to_vanilla")]
    [TestCase(true, Target, NativeBanRoute.HostRoute, TestName = "Host_click_with_a_SteamID64_is_routed")]
    [TestCase(true, "", NativeBanRoute.Refuse, TestName = "Host_click_without_an_id_is_refused_not_guessed")]
    [TestCase(true, "not-an-id", NativeBanRoute.Refuse, TestName = "Host_click_with_a_bad_id_is_refused")]
    public void Ban_buttons_route_only_for_the_host_and_only_with_a_real_id(bool isLocalHost, string steamId, object expected)
    {
        Assert.That(NativeBanRoutePolicy.Decide(isLocalHost, steamId), Is.EqualTo((NativeBanRoute)expected));
    }

    [Test]
    public void All_three_vanilla_ban_buttons_are_routed_through_the_host_check()
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string patch = File.ReadAllText(Path.Combine(root, "src", "Patches", "NativeBanRoutePatch.cs")).Replace("\r\n", "\n");
        int routerStart = patch.IndexOf("internal static bool TryRouteHostBan(", System.StringComparison.Ordinal);
        string router = routerStart < 0 ? string.Empty : patch.Substring(routerStart, patch.IndexOf("\n    }\n", routerStart, System.StringComparison.Ordinal) - routerStart);
        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(TemporaryPhoto), nameof(TemporaryPhoto.ButtonBan))]"));
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(PlayerItemController), nameof(PlayerItemController.ButtonBan))]"));
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(PlayerPanelController), nameof(PlayerPanelController.ButtonBan))]"));
            Assert.That(router, Does.Contain("NativeBanRoutePolicy.Decide(IsVanillaHostGate(), steamId)"), "Routing uses the gate vanilla uses to enable the buttons (PurrNet host); the stricter Steam-owner check refuses visibly inside TryExecuteHostAction.");
            Assert.That(patch, Does.Contain("return textChannelManager.isHost;"));
            Assert.That(patch, Does.Not.Contain("SteamIdModerationController.IsLocalHost()"), "A stricter routing gate would let a host click fall through to vanilla silently.");
            Assert.That(router, Does.Contain("SteamIdModerationController.TryExecuteHostAction(\"ban\", steamId, \"none\", ownerSteamId, out message)"));
            Assert.That(router, Does.Contain("catch (Exception ex)"), "An exception never falls through to vanilla's IndexOf path.");
            Assert.That(patch, Does.Not.Contain("IndexOf("), "The routed path never picks a row by index.");
            Assert.That(patch, Does.Not.Contain("BanRPC("), "Only SteamIdModerationController sends the kick.");
            Assert.That(patch, Does.Contain("if (!handled)"), "Vanilla runs only when the policy said Vanilla.");
            Assert.That(patch.Split("if (!handled)").Length, Is.EqualTo(4), "Each of the three prefixes falls through only when not handled.");
            Assert.That(patch.Split("        return false;\n    }\n}").Length, Is.EqualTo(4), "Each prefix ends by skipping the vanilla method once it handled the click.");
        });
    }
}
