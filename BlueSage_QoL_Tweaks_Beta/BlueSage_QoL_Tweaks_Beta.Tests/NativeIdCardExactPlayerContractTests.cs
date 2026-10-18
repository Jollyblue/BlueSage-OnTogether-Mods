using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NativeIdCardExactPlayerContractTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

    private static string ReadController() =>
        File.ReadAllText(Path.Combine(Root, "src", "NativeIdCardController.cs"));

    [Test]
    public void Exact_transaction_pins_controller_transform_row_and_every_PlayerIDInfo_field()
    {
        string controller = ReadController();

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("internal PlayerController PlayerController;"));
            Assert.That(controller, Does.Contain("internal PlayerIDInfo PlayerInfo;"));
            Assert.That(controller, Does.Contain("currentPlayerController.GetComponent<NetworkTransform>()"));
            Assert.That(controller, Does.Contain("ReferenceEquals(currentControllerTransform, currentTransform)"));
            Assert.That(controller, Does.Contain("ReferenceEquals(panel.PlayerControllers[index], playerController)"));
            Assert.That(controller, Does.Contain("PlayerIdInfoMatches(panel.IDInfos[index], playerInfo)"));

            foreach (string field in new[]
                     {
                         "Name",
                         "DateOfBirth",
                         "HoroscopeSign",
                         "Location",
                         "AreaOfStudy",
                         "FavoriteQuote",
                         "IDCardColorIndex",
                         "IsGold",
                     })
            {
                Assert.That(controller, Does.Contain($".{field}"),
                    $"The exact tuple must pin and revalidate PlayerIDInfo.{field}.");
            }
        });
    }

    [Test]
    public void Exact_controller_path_uses_vanilla_CapturePhoto_and_has_no_index_or_name_fallback()
    {
        string controller = ReadController();

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("TryOpenExactPlayerCard(temporaryPhoto, target)"));
            Assert.That(controller, Does.Contain("temporaryPhoto.CapturePhoto(target.PlayerController)"));
            Assert.That(controller, Does.Contain("currentPlayerController != null"));
            Assert.That(controller, Does.Not.Contain("ButtonOpenIdCheck("));
            Assert.That(controller, Does.Not.Contain("degraded-index-fallback"));
            Assert.That(controller, Does.Not.Contain("ButtonOpenIdCheck(target.DisplayNameRaw"));
            Assert.That(controller, Does.Not.Contain("ButtonOpenIdCheck(playerName"));
            Assert.That(controller, Does.Not.Contain("DisplayName =="));
        });
    }

    [Test]
    public void Sequential_A_then_B_then_C_requests_use_each_newly_resolved_target_without_cached_selection()
    {
        string controller = ReadController();

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("out NativeIdCardTarget target"));
            Assert.That(controller, Does.Contain("TryOpenExactPlayerCard(temporaryPhoto, target)"));
            Assert.That(controller, Does.Not.Match(@"static\s+PlayerController\s+[_A-Za-z]"),
                "A previous A/B selection must never be cached and reused for the next card.");
            Assert.That(controller, Does.Not.Match(@"static\s+NativeIdCardTarget\s+[_A-Za-z]"));
        });
    }

    [Test]
    public void Stale_controller_or_transform_fails_closed_before_vanilla_is_called()
    {
        string controller = ReadController();

        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Contain("ReferenceEquals(currentControllerTransform, currentTransform)"));
            Assert.That(controller, Does.Contain("IsFinalTupleStable(target)"));
            Assert.That(controller, Does.Contain("NativeIdCardResolutionState.FinalTupleChanged"));
            Assert.That(controller, Does.Not.Contain("requestedDisplayName"));
            Assert.That(controller, Does.Not.Contain("ReadIdInfoName"));
        });
    }
}
