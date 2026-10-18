using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class ChalkboardModerationPolicyTests
{
    [Test]
    public void ClearRequest_round_trips_over_a_dedicated_member_data_key()
    {
        const long issued = 1784419200;
        const string boardFingerprint = "0123456789ABCDEF";
        string payload = ChalkboardModerationPolicy.BuildMemberData(
            2,
            boardFingerprint,
            issued,
            "chalkNonce_123");

        Assert.Multiple(() =>
        {
            Assert.That(ChalkboardModerationPolicy.MemberDataKey, Is.EqualTo("bluesage_chalk_request_v2"));
            Assert.That(payload, Is.EqualTo("v2|clear|2|0123456789ABCDEF|1784419200|chalkNonce_123"));
            Assert.That(
                ChalkboardModerationPolicy.TryParseMemberData(payload, out ChalkboardClearRequest request),
                Is.True);
            Assert.That(request.Action, Is.EqualTo("clear"));
            Assert.That(request.BoardIndex, Is.EqualTo(2));
            Assert.That(request.BoardFingerprint, Is.EqualTo(boardFingerprint));
            Assert.That(request.IssuedUnixSeconds, Is.EqualTo(issued));
            Assert.That(request.Nonce, Is.EqualTo("chalkNonce_123"));
            Assert.That(request.SnapshotName, Is.Empty);
        });
    }

    [Test]
    public void RestoreRequest_round_trips_a_safe_host_local_snapshot_name()
    {
        string payload = ChalkboardModerationPolicy.BuildRestoreMemberData(
            1,
            "0123456789ABCDEF",
            1784419200,
            "chalkNonce_123",
            "robin_recovery-1");

        Assert.Multiple(() =>
        {
            Assert.That(
                payload,
                Is.EqualTo("v2|load|1|0123456789ABCDEF|1784419200|chalkNonce_123|robin_recovery-1"));
            Assert.That(
                ChalkboardModerationPolicy.TryParseMemberData(payload, out ChalkboardClearRequest request),
                Is.True);
            Assert.That(request.Action, Is.EqualTo("load"));
            Assert.That(request.BoardIndex, Is.EqualTo(1));
            Assert.That(request.SnapshotName, Is.EqualTo("robin_recovery-1"));
        });
    }

    [TestCase("")]
    [TestCase("v1|clear|0|0123456789ABCDEF|1784419200|chalkNonce_123")]
    [TestCase("v2|clear|0|0123456789ABCDEF|1784419200|short")]
    [TestCase("v2|ban|0|0123456789ABCDEF|1784419200|chalkNonce_123")]
    [TestCase("v2|clear|-1|0123456789ABCDEF|1784419200|chalkNonce_123")]
    [TestCase("v2|clear|3|0123456789ABCDEF|1784419200|chalkNonce_123")]
    [TestCase("v2|clear|0|not-a-fingerprint|1784419200|chalkNonce_123")]
    [TestCase("v2|clear|0|0123456789ABCDEF|not-a-time|chalkNonce_123")]
    [TestCase("v2|clear|0|0123456789ABCDEF|1784419200|chalk nonce")]
    [TestCase("v2|clear|0|0123456789ABCDEF|1784419200|chalkNonce_123|extra")]
    [TestCase("v2|load|0|0123456789ABCDEF|1784419200|chalkNonce_123")]
    [TestCase("v2|load|0|0123456789ABCDEF|1784419200|chalkNonce_123|../escape")]
    public void ClearRequest_rejects_malformed_or_out_of_bounds_payloads(string payload)
    {
        Assert.That(
            ChalkboardModerationPolicy.TryParseMemberData(payload, out _),
            Is.False);
    }

    [Test]
    public void Request_rejects_an_oversized_payload_before_parsing()
    {
        Assert.That(
            ChalkboardModerationPolicy.TryParseMemberData(new string('x', 257), out _),
            Is.False);
    }

    [Test]
    public void ClearRequest_is_short_lived_and_allows_only_two_seconds_of_future_clock_skew()
    {
        const long now = 1784419215;

        Assert.Multiple(() =>
        {
            Assert.That(ChalkboardModerationPolicy.IsFresh(now - 15, now), Is.True);
            Assert.That(ChalkboardModerationPolicy.IsFresh(now - 16, now), Is.False);
            Assert.That(ChalkboardModerationPolicy.IsFresh(now + 1, now), Is.True);
            Assert.That(ChalkboardModerationPolicy.IsFresh(now + 2, now), Is.True);
            Assert.That(ChalkboardModerationPolicy.IsFresh(now + 3, now), Is.False);
        });
    }

    [TestCase("0", 3, true, 0)]
    [TestCase("2", 3, true, 2)]
    [TestCase("3", 4, false, -1)]
    [TestCase("2", 2, false, -1)]
    [TestCase("", 3, false, -1)]
    [TestCase("active", 3, false, -1)]
    [TestCase("-1", 3, false, -1)]
    [TestCase("4", 4, false, -1)]
    [TestCase("0", 0, false, -1)]
    public void Destructive_clear_requires_an_explicit_current_board_index(
        string token,
        int boardCount,
        bool expected,
        int expectedIndex)
    {
        bool result = ChalkboardModerationPolicy.TryParseExplicitBoardIndex(
            token,
            boardCount,
            out int index,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(index, Is.EqualTo(expectedIndex));
            Assert.That(string.IsNullOrWhiteSpace(error), Is.EqualTo(expected));
        });
    }

    [TestCase("Classroom", "Map/School/Chalkboard", "Classroom")]
    [TestCase("Island", "World/River/Chalkboard", "River")]
    [TestCase("Island", "World/Bridge/Board", "River")]
    [TestCase("Island", "World/Unknown/Board", "Unmapped")]
    public void Location_hints_are_helpful_without_inventing_an_unknown_mapping(
        string scene,
        string hierarchy,
        string expected)
    {
        Assert.That(
            ChalkboardModerationPolicy.ClassifyLocation(scene, hierarchy),
            Is.EqualTo(expected));
    }

    [Test]
    public void Board_fingerprint_is_stable_and_scene_specific()
    {
        string first = ChalkboardModerationPolicy.BuildBoardFingerprint(
            "MainIsland",
            "World[0]/River[2]/Chalkboard[1]");
        string second = ChalkboardModerationPolicy.BuildBoardFingerprint(
            "MainIsland",
            "World[0]/River[2]/Chalkboard[1]");
        string other = ChalkboardModerationPolicy.BuildBoardFingerprint(
            "Classroom",
            "World[0]/River[2]/Chalkboard[1]");

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.Match("^[0-9A-F]{16}$"));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        });
    }

    [Test]
    public void Board_fingerprint_quantizes_world_position_and_detects_a_different_surface()
    {
        System.Reflection.MethodInfo? method = typeof(ChalkboardModerationPolicy).GetMethod(
            "BuildBoardFingerprint",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string), typeof(string), typeof(double), typeof(double), typeof(double) },
            modifiers: null);

        Assert.That(method, Is.Not.Null, "The board fingerprint must include quantized world position.");

        string Invoke(double x, double y, double z) =>
            (string)method!.Invoke(null, new object[] { "Island", "World/Board", x, y, z })!;

        string first = Invoke(38.9090, 1.6010, -12.3220);
        string sameQuantizedPosition = Invoke(38.9094, 1.6014, -12.3224);
        string otherSurface = Invoke(38.9190, 1.6010, -12.3220);

        Assert.Multiple(() =>
        {
            Assert.That(first, Does.Match("^[0-9A-F]{16}$"));
            Assert.That(sameQuantizedPosition, Is.EqualTo(first));
            Assert.That(otherSurface, Is.Not.EqualTo(first));
        });
    }

    [Test]
    public void Delivery_result_preserves_counts_and_marks_skips_as_partial()
    {
        Type? resultType = typeof(ChalkboardModerationPolicy).Assembly.GetType(
            "BlueSage.QoLTweaks.Core.ChalkboardDeliveryResult");
        Assert.That(resultType, Is.Not.Null, "A typed delivery result must carry attempted/sent/skipped counts.");

        object Create(int attempted, int sent, int skipped) =>
            Activator.CreateInstance(
                resultType!,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args: new object[] { attempted, sent, skipped },
                culture: null)!;

        object partial = Create(4, 3, 1);
        object complete = Create(3, 3, 0);
        object Read(object instance, string property) =>
            resultType!.GetProperty(
                property,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)!.GetValue(instance)!;

        Assert.Multiple(() =>
        {
            Assert.That(Read(partial, "Attempted"), Is.EqualTo(4));
            Assert.That(Read(partial, "Sent"), Is.EqualTo(3));
            Assert.That(Read(partial, "Skipped"), Is.EqualTo(1));
            Assert.That(Read(partial, "IsComplete"), Is.False);
            Assert.That(Read(complete, "IsComplete"), Is.True);
        });
    }

    [TestCase(0, 38.909, 1.601, -12.322, "River/Stage")]
    [TestCase(1, -19.605, 4.999, 7.912, "Classroom board A (face A)")]
    [TestCase(2, -19.620, 4.999, 7.954, "Classroom board B (face B)")]
    [TestCase(3, 0.0, 0.0, 0.0, "Unmapped")]
    public void Build_24259292_catalog_coordinates_label_the_three_verified_board_surfaces(
        int boardIndex,
        double x,
        double y,
        double z,
        string expected)
    {
        Assert.That(
            ChalkboardModerationPolicy.ClassifyBuild24259292Location(boardIndex, x, y, z),
            Is.EqualTo(expected));
    }

    [Test]
    public void Build_24259292_catalog_rejects_swapped_classroom_faces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ChalkboardModerationPolicy.ClassifyBuild24259292Location(
                    1,
                    -19.620,
                    4.999,
                    7.954),
                Is.EqualTo("Unmapped"));
            Assert.That(
                ChalkboardModerationPolicy.ClassifyBuild24259292Location(
                    2,
                    -19.605,
                    4.999,
                    7.912),
                Is.EqualTo("Unmapped"));
        });
    }
}
