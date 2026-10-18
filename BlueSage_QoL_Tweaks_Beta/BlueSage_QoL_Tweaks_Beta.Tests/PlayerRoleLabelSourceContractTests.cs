using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerRoleLabelSourceContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Public_role_compatibility_heartbeat_and_exact_badges_never_mutate_names()
    {
        string controller = Read("src", "Patches", "PlayerRoleLabelController.cs");
        string badges = Read("src", "Patches", "PlayerBadgeController.cs");
        string patch = Read("src", "Patches", "PlayerRoleLabelPatch.cs");
        string plugin = Read("src", "Plugin.cs");
        string movement = Read("src", "Patches", "OptInRescuePatch.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(patch, Does.Contain("[HarmonyPatch(typeof(PlayerItemController), \"Init\")]"));
            Assert.That(patch, Does.Contain("PlayerBadgeController.RegisterRow"));
            Assert.That(patch, Does.Not.Contain("ShouldApplyEnhancedPlayerPanel"));
            Assert.That(controller, Does.Contain("CompatibleClientPresencePolicy.MemberDataKey"));
            Assert.That(controller, Does.Contain("PlayerBadgeController.Tick();"));
            Assert.That(controller, Does.Not.Contain("NameText"));
            Assert.That(controller, Does.Not.Contain(".text ="));
            Assert.That(badges, Does.Contain("GetLobbyOwner(lobby)"));
            Assert.That(badges, Does.Contain("GetPublishedHelpers("));
            Assert.That(badges, Does.Contain("ModerationCapabilityPolicy.IsFresh"));
            Assert.That(badges, Does.Contain("marker.OwnerRow.IsHost"),
                "Owner migration must use the row's current host state rather than the Init-time snapshot.");
            Assert.That(badges, Does.Not.Contain("state.IsHost,"));
            Assert.That(controller, Does.Not.Contain("PlayerNameField.SetValue"));
            Assert.That(controller, Does.Not.Contain("TryApplyCompanionStyledBase"));
            Assert.That(movement, Does.Contain("PlayerRoleLabelController.Tick();"));
            Assert.That(plugin, Does.Contain("PlayerRoleLabelController.RestoreAll();"));
            Assert.That(plugin, Does.Contain("TrySetCompanionHostRoleLabel"));
            Assert.That(plugin, Does.Contain("TryClearCompanionHostRoleLabel"));
            Assert.That(moderation, Does.Contain("PlayerRoleLabelController.PublishHostHeartbeat"));
            Assert.That(moderation, Does.Contain("PlayerRoleLabelController.ClearPublishedHostLabel"));
        });
    }

    [Test]
    public void Rendered_role_text_is_never_an_authority_input()
    {
        string controller = Read("src", "Patches", "PlayerRoleLabelController.cs");
        string moderation = Read("src", "SteamIdModerationController.cs");
        Assert.Multiple(() =>
        {
            Assert.That(controller, Does.Not.Contain("CanLocalIssue"));
            Assert.That(controller, Does.Not.Contain("IsAuthorizedCurrentIssuer"));
            Assert.That(moderation, Does.Not.Contain("PlayerRoleLabelController.GetRendered"));
        });
    }

    [Test]
    public void Owner_published_creator_host_evidence_requires_the_exact_current_owner_host_row()
    {
        string badges = Read("src", "Patches", "PlayerBadgeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(badges, Does.Contain("PlayerIdentityEvidenceController.TryResolveExactCached("));
            Assert.That(badges, Does.Contain("IsExactCurrentRow("));
            Assert.That(badges, Does.Contain(
                "bool ownerPublishedCreatorHostLabelFresh =\n            ownerMatchesRow &&\n            marker.OwnerRow.IsHost &&\n            !string.IsNullOrWhiteSpace(hostLabel);"));
            Assert.That(badges, Does.Contain(
                "ownerPublishedCreatorHostLabelFresh:\n                ownerPublishedCreatorHostLabelFresh"));
        });
    }

    [Test]
    public void Local_creator_or_private_fleet_host_publication_reauthorizes_exact_provider_truth_on_set_and_heartbeat()
    {
        string controller = Read("src", "Patches", "PlayerRoleLabelController.cs");
        string badges = Read("src", "Patches", "PlayerBadgeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(controller.Split(
                "PlayerBadgeController.TryResolveFreshLocalCreatorContribution(",
                StringSplitOptions.None).Length - 1, Is.EqualTo(2),
                "Initial set and every due heartbeat must independently authorize creator publication.");
            Assert.That(controller, Does.Contain("CreatorHostLabelPublicationPolicy.CanPublish("));
            Assert.That(controller, Does.Contain("CreatorHostLabelPublicationPolicy.DecideHeartbeat("));
            Assert.That(controller.Split(
                "TryResolveFreshPrivateHostAuthority()",
                StringSplitOptions.None).Length - 1, Is.EqualTo(3),
                "Initial set and every due heartbeat must independently reauthorize private fleet host authority through one helper.");
            Assert.That(controller, Does.Contain("BlueSageExtensionRegistry.TryReadHostCapability("));
            Assert.That(controller, Does.Contain("snapshot.State == BlueSageHostCapabilityStateV1.Ready"));
            Assert.That(badges, Does.Contain("internal static bool TryResolveFreshLocalCreatorContribution("));
            Assert.That(badges, Does.Contain("SteamUser.GetSteamID()"));
            Assert.That(badges, Does.Contain("TryResolveFreshIdentityTruthBySteam("));
            Assert.That(badges, Does.Contain("truth.SteamId")
                .And.Contain("truth.PurrNetId")
                .And.Contain("truth.LobbyEpoch")
                .And.Contain("truth.EvidenceGeneration"));
            Assert.That(badges, Does.Contain("BlueSageExtensionRegistry.TryReadBadgeContribution("));
            Assert.That(badges, Does.Contain("contributedBadgePresent = response.ContributedBadgePresent;"));
        });
    }

    [Test]
    public void Provider_loss_clears_and_stops_publication_while_manual_cleanup_stays_ungated()
    {
        string controller = Read("src", "Patches", "PlayerRoleLabelController.cs");
        string setMethod = Slice(
            controller,
            "internal static bool TrySetLocalHostLabelOverride(",
            "internal static bool TryClearLocalHostLabelOverride(");
        string clearMethod = Slice(
            controller,
            "internal static bool TryClearLocalHostLabelOverride(",
            "internal static void PublishHostHeartbeat()");
        string heartbeatMethod = Slice(
            controller,
            "internal static void PublishHostHeartbeat()",
            "internal static void ClearPublishedHostLabel()");
        string cleanupMethod = Slice(
            controller,
            "internal static void ClearPublishedHostLabel()",
            "internal static void ClearPublishedStateForShutdown(");

        Assert.Multiple(() =>
        {
            Assert.That(setMethod, Does.Contain("StopAndClearPublishedHostLabel("));
            Assert.That(heartbeatMethod, Does.Contain("CreatorHostLabelHeartbeatDecision.ClearAndStop"));
            Assert.That(heartbeatMethod, Does.Contain("StopAndClearPublishedHostLabel("));
            Assert.That(clearMethod, Does.Not.Contain("TryResolveFreshLocalCreatorContribution"));
            Assert.That(cleanupMethod, Does.Not.Contain("TryResolveFreshLocalCreatorContribution"));
            Assert.That(controller, Does.Contain("_localHostLabelOverride = string.Empty;")
                .And.Contain("_nextRolePublishAt = 0f;"));
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
        Assert.That(end, Is.GreaterThan(start), endMarker);
        return source.Substring(start, end - start);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
