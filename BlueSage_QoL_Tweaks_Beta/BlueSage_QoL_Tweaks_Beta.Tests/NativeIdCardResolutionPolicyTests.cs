using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class NativeIdCardResolutionPolicyTests
{
    private const string SteamId = "76561198012345678";
    private const string PlayerId = "PurrNet-42";

    [Test]
    public void Exact_fresh_Steam_PurrNet_Unity_and_native_row_agreement_opens_only_the_current_index()
    {
        NativeIdCardResolutionDecision decision = NativeIdCardResolutionPolicy.Evaluate(Valid());

        Assert.Multiple(() =>
        {
            Assert.That(decision.State, Is.EqualTo(NativeIdCardResolutionState.Ready));
            Assert.That(decision.PlayerIndex, Is.EqualTo(2));
            Assert.That(decision.MayOpenNativeCard, Is.True);
        });
    }

    [TestCase("OrdinaryUser")]
    [TestCase("Host")]
    [TestCase("AuthenticatedHelper")]
    [TestCase("BluePrivateConsumer")]
    public void Every_caller_kind_requires_the_same_exact_evidence(
        string callerKindName)
    {
        NativeIdCardCallerKind callerKind = Enum.Parse<NativeIdCardCallerKind>(callerKindName);
        NativeIdCardResolutionContext context = Valid();
        context.CallerKind = callerKind;

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(NativeIdCardResolutionState.Ready));

        context.CurrentRowSteamId = "76561198099999999";
        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(NativeIdCardResolutionState.RowSteamIdentityChanged));
    }

    [TestCase(-1, 2, 2, "ExpectedIndexUnavailable")]
    [TestCase(1, 2, 2, "StaleExpectedIndex")]
    [TestCase(2, 2, 1, "NativeRowIndexMismatch")]
    public void Stale_minimap_or_reused_row_indices_fail_closed(
        int expectedIndex,
        int resolvedIndex,
        int currentRowIndex,
        string expectedStateName)
    {
        NativeIdCardResolutionState expectedState = Enum.Parse<NativeIdCardResolutionState>(expectedStateName);
        NativeIdCardResolutionContext context = Valid();
        context.ExpectedIndex = expectedIndex;
        context.FreshResolvedIndex = resolvedIndex;
        context.CurrentRowIndex = currentRowIndex;

        NativeIdCardResolutionDecision decision = NativeIdCardResolutionPolicy.Evaluate(context);

        Assert.Multiple(() =>
        {
            Assert.That(decision.State, Is.EqualTo(expectedState));
            Assert.That(decision.MayOpenNativeCard, Is.False);
            Assert.That(decision.PlayerIndex, Is.EqualTo(-1));
        });
    }

    [TestCase(false, true, SteamId, PlayerId, PlayerId, "FreshEvidenceUnavailable")]
    [TestCase(true, false, SteamId, PlayerId, PlayerId, "NativeRowReferenceMismatch")]
    [TestCase(true, true, "76561198099999999", PlayerId, PlayerId, "RowSteamIdentityChanged")]
    [TestCase(true, true, SteamId, "", PlayerId, "PurrNetIdentityUnavailable")]
    [TestCase(true, true, SteamId, "PurrNet-77", PlayerId, "PurrNetIdentityMismatch")]
    public void Split_or_reused_identity_evidence_never_opens_the_card(
        bool freshResolution,
        bool rowReferenceMatches,
        string currentRowSteamId,
        string currentRowPlayerId,
        string freshPlayerId,
        string expectedStateName)
    {
        NativeIdCardResolutionState expectedState = Enum.Parse<NativeIdCardResolutionState>(expectedStateName);
        NativeIdCardResolutionContext context = Valid();
        context.FreshResolutionSucceeded = freshResolution;
        context.CurrentRowReferenceMatches = rowReferenceMatches;
        context.CurrentRowSteamId = currentRowSteamId;
        context.CurrentRowPlayerId = currentRowPlayerId;
        if (!string.IsNullOrWhiteSpace(currentRowPlayerId))
        {
            context.ExpectedPlayerId = currentRowPlayerId;
        }
        context.FreshResolvedPlayerId = freshPlayerId;

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(expectedState));
    }

    [Test]
    public void A_missing_Unity_network_transform_fails_closed()
    {
        NativeIdCardResolutionContext context = Valid();
        context.HasLiveNetworkTransform = false;

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(NativeIdCardResolutionState.UnityTransformUnavailable));
    }

    [Test]
    public void A_two_second_old_surface_with_a_changed_PurrNet_identity_fails_closed()
    {
        NativeIdCardResolutionContext context = Valid();
        context.ExpectedPlayerId = "PurrNet-previous";

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(NativeIdCardResolutionState.ObservedPurrNetIdentityChanged));
    }

    [TestCase(false, true, true, "NativeListsMisaligned")]
    [TestCase(true, false, true, "UnityTransformIdentityChanged")]
    [TestCase(true, true, false, "FinalTupleChanged")]
    public void List_or_tuple_mutation_between_click_and_native_invocation_fails_closed(
        bool nativeListsAligned,
        bool expectedTransformMatches,
        bool finalTupleStable,
        string expectedStateName)
    {
        NativeIdCardResolutionState expectedState = Enum.Parse<NativeIdCardResolutionState>(expectedStateName);
        NativeIdCardResolutionContext context = Valid();
        context.NativeListsAligned = nativeListsAligned;
        context.ExpectedNetworkTransformMatches = expectedTransformMatches;
        context.FinalTupleStable = finalTupleStable;

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(expectedState));
    }

    [Test]
    public void Native_roster_shape_accepts_extra_inactive_pooled_rows()
    {
        Assert.That(
            NativeRosterShapePolicy.HasAlignedActivePrefix(
                steamIdCount: 2,
                playerIdCount: 2,
                transformCount: 2,
                idInfoCount: 2,
                pooledRowCount: 3),
            Is.True);
    }

    [Test]
    public void Native_roster_shape_rejects_fewer_rows_than_the_active_prefix()
    {
        Assert.That(
            NativeRosterShapePolicy.HasAlignedActivePrefix(
                steamIdCount: 2,
                playerIdCount: 2,
                transformCount: 2,
                idInfoCount: 2,
                pooledRowCount: 1),
            Is.False);
    }

    [Test]
    public void A_panel_close_callback_that_changes_the_tuple_fails_before_native_open()
    {
        bool tupleStable = true;
        bool panelClosed = false;
        bool nativeCardOpened = false;
        int tupleChecks = 0;

        NativeIdCardInvocationState state =
            NativeIdCardInvocationGuard.Invoke(
                () =>
                {
                    tupleChecks++;
                    return tupleStable;
                },
                () =>
                {
                    panelClosed = true;
                    tupleStable = false;
                },
                () => nativeCardOpened = true);

        Assert.Multiple(() =>
        {
            Assert.That(
                state,
                Is.EqualTo(
                    NativeIdCardInvocationState.TupleChangedAfterPanelClose));
            Assert.That(panelClosed, Is.True);
            Assert.That(nativeCardOpened, Is.False);
            Assert.That(tupleChecks, Is.EqualTo(2));
        });
    }

    // UAT F3FAB760BEB5 (2026-10-17): vanilla pools player-list rows and re-stamps their
    // _playerSteamId only in UpdateServerPanel(), which runs only while the list is open. A leave
    // with the list closed shifts PlayerSteamIDs, so rows after the leaver keep a stale Steam ID.
    // When the authoritative list entry still matches the click, the row is stale, not reassigned.
    [Test]
    public void Stale_pooled_row_whose_list_entry_still_matches_is_refreshable_not_a_new_identity()
    {
        NativeIdCardResolutionContext context = Valid();
        context.CurrentRowSteamId = "76561198099999999";
        context.CurrentRowListSteamId = SteamId;

        NativeIdCardResolutionDecision decision = NativeIdCardResolutionPolicy.Evaluate(context);

        Assert.Multiple(() =>
        {
            Assert.That(decision.State, Is.EqualTo(NativeIdCardResolutionState.NativeRowStaleRefreshable));
            Assert.That(decision.MayOpenNativeCard, Is.False, "Refreshable still never opens without a re-check.");
            Assert.That(decision.PlayerIndex, Is.EqualTo(-1));
        });
    }

    [Test]
    public void Row_and_list_both_pointing_at_another_player_still_refuses_as_identity_changed()
    {
        NativeIdCardResolutionContext context = Valid();
        context.CurrentRowSteamId = "76561198099999999";
        context.CurrentRowListSteamId = "76561198099999999";

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(NativeIdCardResolutionState.RowSteamIdentityChanged));
    }

    [Test]
    public void Stale_row_is_never_refreshable_when_the_clicked_index_is_itself_stale()
    {
        NativeIdCardResolutionContext context = Valid();
        context.CurrentRowSteamId = "76561198099999999";
        context.CurrentRowListSteamId = SteamId;
        context.ExpectedIndex = 1;

        Assert.That(
            NativeIdCardResolutionPolicy.Evaluate(context).State,
            Is.EqualTo(NativeIdCardResolutionState.StaleExpectedIndex));
    }

    private static NativeIdCardResolutionContext Valid() => new()
    {
        CallerKind = NativeIdCardCallerKind.OrdinaryUser,
        RequestedSteamId = SteamId,
        FreshResolutionSucceeded = true,
        FreshResolvedSteamId = SteamId,
        FreshResolvedPlayerId = PlayerId,
        ExpectedPlayerId = PlayerId,
        FreshResolvedIndex = 2,
        ExpectedIndex = 2,
        CurrentRowIndex = 2,
        CurrentRowReferenceMatches = true,
        CurrentRowSteamId = SteamId,
        CurrentRowPlayerId = PlayerId,
        HasLiveNetworkTransform = true,
        NativeListsAligned = true,
        ExpectedNetworkTransformMatches = true,
        FinalTupleStable = true,
    };
}
