using System.Reflection;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerBadgeLayoutPolicyTests
{
    [Test]
    public void Complete_role_tokens_render_adjacent_without_spending_name_width_on_spaces()
    {
        Assert.That(
            PlayerBadgeRenderPolicy.RenderCompactRichText("[O] [U] [♪]"),
            Is.EqualTo(
                "<color=#70D6FF>[O]</color>" +
                "<color=#F4A261>[U]</color>" +
                "<color=#C29BFF>[♪]</color>"));
    }

    [Test]
    public void Compact_host_row_keeps_full_name_budget_and_all_three_badges()
    {
        PlayerBadgeAllocation allocation =
            PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation(
                nativeNameWidth: 420f,
                nativePreferredWidth: 292f,
                minimumReadableNameWidth: 160f,
                horizontalPadding: 3f,
                hostPreferredWidth: 0f,
                segmentGap: 4f,
                nativeNameGap: 4f,
                preferredWidthsByCapacity: new[] { 34f, 68f, 102f });

        Assert.Multiple(() =>
        {
            Assert.That(allocation.BadgeCapacity, Is.EqualTo(3));
            Assert.That(allocation.NativeNameWidth, Is.EqualTo(292f));
            Assert.That(allocation.OwnedLaneWidth, Is.EqualTo(108f));
        });
    }

    [Test]
    public void Live_helper_row_geometry_keeps_the_full_native_identity_and_all_three_tags()
    {
        PlayerBadgeAllocation allocation =
            PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation(
                nativeNameWidth: 458.4f,
                nativePreferredWidth: 355.6f,
                minimumReadableNameWidth: 160f,
                horizontalPadding: 0f,
                hostPreferredWidth: 0f,
                segmentGap: 4f,
                nativeNameGap: 0f,
                preferredWidthsByCapacity: new[] { 39.7f, 73.8f, 101.9f });

        Assert.Multiple(() =>
        {
            Assert.That(allocation.BadgeCapacity, Is.EqualTo(3));
            Assert.That(allocation.NativeNameWidth, Is.EqualTo(355.6f));
            Assert.That(allocation.OwnedLaneWidth, Is.EqualTo(101.9f));
        });
    }

    [Test]
    public void Live_solo_host_requests_only_the_missing_container_width_for_complete_identity_and_badges()
    {
        float expansion =
            PlayerBadgeLayoutPolicy.ResolveRequiredContainerExpansion(
                nativeNameWidth: 458.4f,
                nativePreferredWidth: 466.3f,
                horizontalPadding: 0f,
                hostPreferredWidth: 0f,
                segmentGap: 4f,
                nativeNameGap: 0f,
                fullBadgePreferredWidth: 100.1f,
                maximumExpansion: 180f);

        PlayerBadgeAllocation allocation =
            PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation(
                nativeNameWidth: 458.4f + expansion,
                nativePreferredWidth: 466.3f,
                minimumReadableNameWidth: 160f,
                horizontalPadding: 0f,
                hostPreferredWidth: 0f,
                segmentGap: 4f,
                nativeNameGap: 0f,
                preferredWidthsByCapacity: new[] { 39.7f, 72f, 100.1f });

        Assert.Multiple(() =>
        {
            Assert.That(expansion, Is.EqualTo(108f).Within(0.01f));
            Assert.That(allocation.BadgeCapacity, Is.EqualTo(3));
            Assert.That(allocation.NativeNameWidth, Is.EqualTo(466.3f).Within(0.01f));
            Assert.That(allocation.OwnedLaneWidth, Is.EqualTo(100.1f));
            Assert.That(allocation.UseOverflowLane, Is.False);
        });
    }

    [Test]
    public void Container_expansion_is_bounded_for_pathological_names()
    {
        Assert.That(
            PlayerBadgeLayoutPolicy.ResolveRequiredContainerExpansion(
                nativeNameWidth: 300f,
                nativePreferredWidth: 900f,
                horizontalPadding: 6f,
                hostPreferredWidth: 80f,
                segmentGap: 8f,
                nativeNameGap: 6f,
                fullBadgePreferredWidth: 140f,
                maximumExpansion: 180f),
            Is.EqualTo(180f));
    }

    [Test]
    public void Wide_solo_host_row_keeps_a_readable_name_and_shows_all_three_complete_badges()
    {
        PlayerBadgeAllocation allocation =
            PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation(
                nativeNameWidth: 420f,
                nativePreferredWidth: 300f,
                minimumReadableNameWidth: 160f,
                horizontalPadding: 6f,
                hostPreferredWidth: 42f,
                segmentGap: 8f,
                nativeNameGap: 6f,
                preferredWidthsByCapacity: new[] { 40f, 84f, 128f });

        Assert.Multiple(() =>
        {
            Assert.That(allocation.BadgeCapacity, Is.EqualTo(3));
            Assert.That(allocation.NativeNameWidth, Is.GreaterThanOrEqualTo(160f));
            Assert.That(allocation.OwnedLaneWidth, Is.EqualTo(190f));
        });
    }

    [Test]
    public void Narrow_row_keeps_the_readable_name_floor_and_uses_truthful_single_badge_overflow()
    {
        PlayerBadgeAllocation allocation =
            PlayerBadgeLayoutPolicy.ResolveResponsiveAllocation(
                nativeNameWidth: 250f,
                nativePreferredWidth: 220f,
                minimumReadableNameWidth: 160f,
                horizontalPadding: 6f,
                hostPreferredWidth: 0f,
                segmentGap: 8f,
                nativeNameGap: 6f,
                preferredWidthsByCapacity: new[] { 40f, 84f, 128f });

        Assert.Multiple(() =>
        {
            Assert.That(allocation.BadgeCapacity, Is.EqualTo(1));
            Assert.That(allocation.NativeNameWidth, Is.EqualTo(192f));
            Assert.That(allocation.OwnedLaneWidth, Is.EqualTo(52f));
        });
    }

    [TestCase(420f, 232f, 44f, 232f)]
    [TestCase(300f, 310f, 44f, 256f)]
    [TestCase(250f, 150f, 44f, 150f)]
    [TestCase(0f, 150f, 44f, 0f)]
    [TestCase(float.NaN, 150f, 44f, 0f)]
    public void Name_lane_reserves_measured_identity_before_the_compact_owned_lane(
        float nativeWidth,
        float nativePreferredWidth,
        float minimumOwnedLaneWidth,
        float expected)
    {
        Assert.That(
            ResolveNativeNameWidth(
                nativeWidth,
                nativePreferredWidth,
                minimumOwnedLaneWidth),
            Is.EqualTo(expected));
    }

    [Test]
    public void Ten_character_name_status_and_spoons_keep_their_measured_readable_width()
    {
        const string playerName = "BlueSage10";
        const string status = "Exploring!";
        const string spoons = "[2/5SP]";
        const float averageGlyphWidth = 8f;
        float preferredNativeWidth =
            (playerName.Length + status.Length + spoons.Length + 2) *
            averageGlyphWidth;

        float reservation = ResolveNativeNameWidth(
            nativeWidth: 420f,
            nativePreferredWidth: preferredNativeWidth,
            minimumOwnedLaneWidth: 44f);

        Assert.Multiple(() =>
        {
            Assert.That(playerName, Has.Length.EqualTo(10));
            Assert.That(status, Has.Length.EqualTo(10));
            Assert.That(spoons, Is.EqualTo("[2/5SP]"));
            Assert.That(reservation, Is.EqualTo(preferredNativeWidth));
        });
    }

    [TestCase(220f, 6f, 64f, 8f, 3)]
    [TestCase(180f, 6f, 64f, 8f, 2)]
    [TestCase(120f, 6f, 64f, 8f, 1)]
    [TestCase(110f, 6f, 64f, 8f, 0)]
    [TestCase(0f, 6f, 64f, 8f, 0)]
    public void Badge_capacity_preserves_the_host_then_selects_only_complete_token_sets(
        float maximumLaneWidth,
        float horizontalPadding,
        float hostPreferredWidth,
        float segmentGap,
        int expectedCapacity)
    {
        float[] preferredWidthsByCapacity = { 34f, 74f, 112f };

        int capacity = ResolveBadgeCapacity(
            maximumLaneWidth,
            horizontalPadding,
            hostPreferredWidth,
            segmentGap,
            preferredWidthsByCapacity);

        Assert.That(capacity, Is.EqualTo(expectedCapacity));
    }

    [Test]
    public void Measured_badges_define_the_lane_instead_of_a_fixed_overlay()
    {
        object layout = Resolve(
            hostPreferredWidth: 0f,
            badgePreferredWidth: 92f,
            maximumLaneWidth: 300f,
            horizontalPadding: 6f,
            segmentGap: 8f,
            nativeNameGap: 6f);

        Assert.Multiple(() =>
        {
            Assert.That(ReadFloat(layout, "LaneWidth"), Is.EqualTo(104f));
            Assert.That(ReadFloat(layout, "NativeNameReservation"), Is.EqualTo(110f));
            Assert.That(ReadFloat(layout, "BadgeStart"), Is.EqualTo(6f));
            Assert.That(ReadFloat(layout, "BadgeWidth"), Is.EqualTo(92f));
        });
    }

    [Test]
    public void Host_and_badge_sublanes_are_disjoint()
    {
        object layout = Resolve(64f, 92f, 300f, 6f, 8f, 6f);

        float hostEnd = ReadFloat(layout, "HostStart") +
                        ReadFloat(layout, "HostWidth");
        float badgeStart = ReadFloat(layout, "BadgeStart");

        Assert.Multiple(() =>
        {
            Assert.That(ReadFloat(layout, "LaneWidth"), Is.EqualTo(176f));
            Assert.That(hostEnd, Is.EqualTo(70f));
            Assert.That(badgeStart, Is.EqualTo(78f));
            Assert.That(hostEnd, Is.LessThanOrEqualTo(badgeStart));
        });
    }

    [Test]
    public void Empty_host_omits_the_inter_segment_gap()
    {
        object layout = Resolve(0f, 92f, 300f, 6f, 8f, 6f);

        Assert.Multiple(() =>
        {
            Assert.That(ReadFloat(layout, "HostWidth"), Is.Zero);
            Assert.That(ReadFloat(layout, "BadgeStart"), Is.EqualTo(6f));
            Assert.That(ReadFloat(layout, "LaneWidth"), Is.EqualTo(104f));
        });
    }

    [Test]
    public void Capacity_clamp_preserves_the_full_host_before_using_the_remaining_badge_width()
    {
        object layout = Resolve(64f, 92f, 120f, 6f, 8f, 6f);

        Assert.Multiple(() =>
        {
            Assert.That(ReadFloat(layout, "LaneWidth"), Is.EqualTo(120f));
            Assert.That(ReadFloat(layout, "NativeNameReservation"), Is.EqualTo(120f));
            Assert.That(ReadFloat(layout, "HostWidth"), Is.EqualTo(64f));
            Assert.That(ReadFloat(layout, "BadgeWidth"), Is.EqualTo(36f));
            Assert.That(ReadFloat(layout, "BadgeStart") + ReadFloat(layout, "BadgeWidth"),
                Is.LessThanOrEqualTo(120f));
        });
    }

    [TestCase(180f)]
    [TestCase(240f)]
    [TestCase(360f)]
    [TestCase(720f)]
    public void Canvas_capacity_above_measured_width_does_not_scale_the_lane(float capacity)
    {
        object layout = Resolve(64f, 92f, capacity, 6f, 8f, 6f);

        Assert.That(ReadFloat(layout, "LaneWidth"), Is.EqualTo(176f));
    }

    [Test]
    public void Empty_presentation_reserves_no_native_name_space()
    {
        object layout = Resolve(0f, 0f, 300f, 6f, 8f, 6f);

        Assert.Multiple(() =>
        {
            Assert.That(ReadFloat(layout, "LaneWidth"), Is.Zero);
            Assert.That(ReadFloat(layout, "NativeNameReservation"), Is.Zero);
        });
    }

    [Test]
    public void Unchanged_tick_reuses_the_cached_measured_layout()
    {
        object cache = CreateCache();
        object key = CacheKey("[O] [U] [♪]", "(Testing)", 101, 202, 420f, 1.25f);
        object measured = Resolve(64f, 92f, 300f, 6f, 8f, 6f);
        Store(cache, key, measured);

        bool hit = TryGet(cache, key, out object cached);

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.True);
            Assert.That(ReadFloat(cached, "LaneWidth"), Is.EqualTo(176f));
            Assert.That(ReadFloat(cached, "NativeNameReservation"), Is.EqualTo(182f));
        });
    }

    [TestCase("badge")]
    [TestCase("host")]
    [TestCase("measurement")]
    [TestCase("geometry")]
    [TestCase("width")]
    [TestCase("scale")]
    [TestCase("native-text")]
    public void Material_key_change_requires_remeasurement(string changedField)
    {
        object cache = CreateCache();
        object original = CacheKey("[O] [U] [♪]", "(Testing)", 101, 202, 420f, 1.25f);
        object measured = Resolve(64f, 92f, 300f, 6f, 8f, 6f);
        Store(cache, original, measured);
        object changed = CacheKey(
            changedField == "badge" ? "[O] [U]" : "[O] [U] [♪]",
            changedField == "host" ? "(Host)" : "(Testing)",
            changedField == "measurement" ? 102 : 101,
            changedField == "geometry" ? 203 : 202,
            changedField == "width" ? 421f : 420f,
            changedField == "scale" ? 1.5f : 1.25f,
            changedField == "native-text"
                ? "BlueSage10 Exploring! [3/5SP]"
                : "BlueSage10 Exploring! [2/5SP]");

        bool hit = TryGet(cache, changed, out _);

        Assert.That(hit, Is.False);
    }

    [Test]
    public void Storing_a_changed_key_replaces_the_previous_measurement()
    {
        object cache = CreateCache();
        object original = CacheKey("[O] [U] [♪]", "(Testing)", 101, 202, 420f, 1.25f);
        object changed = CacheKey("[O] [U]", "(Testing)", 101, 202, 420f, 1.25f);
        Store(cache, original, Resolve(64f, 92f, 300f, 6f, 8f, 6f));

        Store(cache, changed, Resolve(64f, 60f, 300f, 6f, 8f, 6f));

        Assert.Multiple(() =>
        {
            Assert.That(TryGet(cache, original, out _), Is.False);
            Assert.That(TryGet(cache, changed, out object cached), Is.True);
            Assert.That(ReadFloat(cached, "LaneWidth"), Is.EqualTo(144f));
        });
    }

    private static object Resolve(
        float hostPreferredWidth,
        float badgePreferredWidth,
        float maximumLaneWidth,
        float horizontalPadding,
        float segmentGap,
        float nativeNameGap)
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeLayoutPolicy");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeLayoutPolicy production type is missing.");
        }

        MethodInfo? method = type!.GetMethod(
            "Resolve",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Assert.Fail("PlayerBadgeLayoutPolicy.Resolve production method is missing.");
        }

        return method!.Invoke(null, new object[]
        {
            hostPreferredWidth,
            badgePreferredWidth,
            maximumLaneWidth,
            horizontalPadding,
            segmentGap,
            nativeNameGap,
        })!;
    }

    private static float ResolveNativeNameWidth(
        float nativeWidth,
        float nativePreferredWidth,
        float minimumOwnedLaneWidth)
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeLayoutPolicy");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeLayoutPolicy production type is missing.");
        }

        MethodInfo? method = type!.GetMethod(
            "ResolveNativeNameWidth",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Assert.Fail("PlayerBadgeLayoutPolicy.ResolveNativeNameWidth production method is missing.");
        }

        return (float)method!.Invoke(null, new object[]
        {
            nativeWidth,
            nativePreferredWidth,
            minimumOwnedLaneWidth,
        })!;
    }

    private static int ResolveBadgeCapacity(
        float maximumLaneWidth,
        float horizontalPadding,
        float hostPreferredWidth,
        float segmentGap,
        IReadOnlyList<float> preferredWidthsByCapacity)
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeLayoutPolicy");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeLayoutPolicy production type is missing.");
        }

        MethodInfo? method = type!.GetMethod(
            "ResolveBadgeCapacity",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Assert.Fail("PlayerBadgeLayoutPolicy.ResolveBadgeCapacity production method is missing.");
        }

        return (int)method!.Invoke(null, new object[]
        {
            maximumLaneWidth,
            horizontalPadding,
            hostPreferredWidth,
            segmentGap,
            preferredWidthsByCapacity,
        })!;
    }

    private static object CreateCache()
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeLayoutMeasurementCache");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeLayoutMeasurementCache production type is missing.");
        }

        return Activator.CreateInstance(type!, nonPublic: true)!;
    }

    private static object CacheKey(
        string badgeText,
        string hostText,
        int measurementFingerprint,
        int geometryFingerprint,
        float nativeRectWidth,
        float scale,
        string nativeText = "BlueSage10 Exploring! [2/5SP]")
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeLayoutCacheKey");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeLayoutCacheKey production type is missing.");
        }

        return Activator.CreateInstance(
            type!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[]
            {
                badgeText,
                hostText,
                nativeText,
                measurementFingerprint,
                geometryFingerprint,
                nativeRectWidth,
                scale,
            },
            culture: null)!;
    }

    private static void Store(object cache, object key, object layout)
    {
        Invoke(cache, "Store", key, layout);
    }

    private static bool TryGet(
        object cache,
        object key,
        out object layout)
    {
        object?[] arguments = { key, null };
        bool hit = (bool)Invoke(cache, "TryGet", arguments)!;
        layout = arguments[1]!;
        return hit;
    }

    private static object? Invoke(
        object target,
        string methodName,
        params object?[] arguments)
    {
        MethodInfo? method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Assert.Fail($"Cache method {methodName} is missing.");
        }

        return method!.Invoke(target, arguments);
    }

    private static float ReadFloat(object value, string propertyName)
    {
        PropertyInfo? property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            Assert.Fail($"Layout property {propertyName} is missing.");
        }

        return (float)property!.GetValue(value)!;
    }
}
