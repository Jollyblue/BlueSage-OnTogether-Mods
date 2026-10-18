using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Runtime wiring for the accessibility fishing aid (Blue, 2026-10-17). Unity cannot run under NUnit,
// so these pin the glue; FishingAidPolicyTests hold the decisions.
public sealed class FishingAidWiringContractTests
{
    [Test]
    public void Aid_learns_from_one_cast_by_hand_and_drives_the_vanilla_fishing_loop()
    {
        string patches = ReadSource("Patches", "FishingAidPatches.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patches, Does.Contain("[HarmonyPatch(typeof(FishingManager), \"CastFishingRod\")]"));
            Assert.That(patches, Does.Contain("[HarmonyPatch(typeof(FishingManager), \"Update\")]"));
            Assert.That(patches, Does.Contain("[HarmonyPatch(typeof(FishingManager), \"FishMiniGameUpdate\")]"));
            Assert.That(patches, Does.Contain("FishingAidPolicy.ShouldStartCast("));
            Assert.That(patches, Does.Contain("FishingAidPolicy.ShouldAnswerBite("));
            Assert.That(patches, Does.Contain("FishingAidPolicy.ShouldClickReelRound("));
            Assert.That(patches, Does.Contain("FishingAidPolicy.ShouldClickLastRound("));
            Assert.That(patches, Does.Contain("FishingAidPolicy.IsFishInZone("));
            Assert.That(patches, Does.Contain("FishingAidPolicy.ShouldForgetCast("));
        });
    }

    [Test]
    public void No_first_cast_needed_the_aid_finds_water_with_the_games_own_cast_ray()
    {
        string patches = ReadSource("Patches", "FishingAidPatches.cs");
        string aim = patches.Substring(patches.IndexOf("internal static bool TryAimAtWater(", System.StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(aim, Does.Contain("FishingAidPolicy.CandidateCastOffsets(forward.x, forward.z, maxRange, minRange)"));
            Assert.That(aim, Does.Contain("manager.FishingRodSettings.CastRange"));
            Assert.That(aim, Does.Contain("_castingRay.Invoke(manager, null)"));
            Assert.That(patches, Does.Contain("FishingAidAccess.TryAimAtWater(manager, PlayerForward())"));
            Assert.That(patches, Does.Contain("IsPlayerIdle()"), "Only casts while standing still.");
            Assert.That(patches, Does.Not.Contain("Fishing Aid paused: out of bait."), "Owned bait never runs out with Reusable Consumables.");
        });
    }

    // Review 2026-10-17: a refused cast must not turn into a click every frame, and the click must
    // reach the game even if the getter is inlined.
    [Test]
    public void Refused_casts_back_off_and_clicks_reach_the_game_two_ways()
    {
        string patches = ReadSource("Patches", "FishingAidPatches.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patches, Does.Contain("if (!_castAttemptPending &&"));
            Assert.That(patches, Does.Contain("_idleSince = now;"), "One attempt per pause.");
            Assert.That(patches, Does.Contain("IsCastAllowedNow()"));
            Assert.That(patches, Does.Contain("input.MouseOverUIObject == null"));
            Assert.That(patches, Does.Contain("ResolutionMode.Desk"));
            Assert.That(patches, Does.Contain("_refusedCasts >= RefusalsBeforeNotice"));
            Assert.That(patches, Does.Contain("AccessTools.PropertySetter(typeof(InputManager), nameof(InputManager.IsMouseButton0Down))"));
            Assert.That(patches, Does.Contain("(_hasRecordedCast && FishingAidAccess.TryAimRecordedCast(manager, _recordedSway)) ||"));
            Assert.That(patches, Does.Contain("now >= _refusedBackoffUntil &&"), "Refused casts back off on their own timer, not the no-water stop.");
            Assert.That(patches, Does.Contain("Fishing Aid: no water found nearby. Try again"));
        });
    }

    [Test]
    public void Fake_clicks_exist_only_for_the_frame_the_fishing_code_reads_them()
    {
        string patches = ReadSource("Patches", "FishingAidPatches.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patches, Does.Contain("[HarmonyPatch(typeof(InputManager), nameof(InputManager.IsMouseButton0Down), MethodType.Getter)]"));
            Assert.That(patches, Does.Contain("[HarmonyPatch(typeof(InputManager), nameof(InputManager.IsMouseButton0Up), MethodType.Getter)]"));
            Assert.That(patches, Does.Contain("FishingAidRuntime.FakeClickFrame == Time.frameCount"));
            Assert.That(patches, Does.Contain("FishingAidRuntime.FakeReleaseFrame == Time.frameCount"));
            Assert.That(patches, Does.Not.Contain("IsSpaceKeyDown"), "Never presses Release: the catch is kept, like doing nothing.");
            Assert.That(patches, Does.Contain("catch (Exception"), "A changed game field disables the aid, never fishing.");
        });
    }

    [Test]
    public void Aid_is_off_by_default_with_a_toggle_settings_and_command()
    {
        string plugin = ReadSource("Plugin.cs");
        string menu = ReadSource("QolMenuWindow.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("EnableFishingAid = Config.Bind(\"Accessibility\", \"EnableFishingAid\", FishingAidPolicy.DefaultEnabled"));
            Assert.That(plugin, Does.Contain("FishingAidReactionSeconds = Config.Bind(\"Accessibility\", \"FishingAidReactionSeconds\", FishingAidPolicy.DefaultReactionSeconds"));
            Assert.That(plugin, Does.Contain("FishingAidClicksPerSecond = Config.Bind(\"Accessibility\", \"FishingAidClicksPerSecond\", FishingAidPolicy.DefaultClicksPerSecond"));
            Assert.That(plugin, Does.Contain("\"fishaid\","));
            Assert.That(menu, Does.Contain("DrawFeatureSection(\"Accessibility\""));
            Assert.That(menu, Does.Contain("DrawToggle(\"Fishing Aid\", Plugin.EnableFishingAid"));
            Assert.That(menu, Does.Contain("DrawTextSetting(\"Fishing Aid bite reaction (seconds)\""));
            Assert.That(menu, Does.Contain("DrawTextSetting(\"Fishing Aid last-round clicks per second\""));
        });
    }

    private static string ReadSource(params string[] parts)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        string path = Path.Combine(root, "src");
        foreach (string part in parts) path = Path.Combine(path, part);
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }
}
