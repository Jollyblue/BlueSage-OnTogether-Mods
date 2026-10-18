using System.Reflection;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerBadgeNativeTextPresentationLeaseTests
{
    private const int BaselineWrap = 4;
    private const int BaselineOverflow = 6;
    private const int NoWrap = 10;
    private const int Ellipsis = 11;

    [Test]
    public void Apply_reserves_right_margin_and_owns_wrap_and_overflow()
    {
        object lease = CreateLease();
        object baseline = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);

        object applied = Apply(lease, baseline, 110f);

        AssertState(applied, 1f, 2f, 113f, 4f, NoWrap, Ellipsis);
    }

    [Test]
    public void Reapplying_uses_the_original_baseline_instead_of_compounding_margin()
    {
        object lease = CreateLease();
        object baseline = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        object first = Apply(lease, baseline, 110f);

        object second = Apply(lease, first, 96f);

        AssertState(second, 1f, 2f, 99f, 4f, NoWrap, Ellipsis);
    }

    [Test]
    public void Disable_then_destroy_restoration_is_exact_and_idempotent()
    {
        object lease = CreateLease();
        object baseline = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        object applied = Apply(lease, baseline, 110f);

        object disabled = Restore(lease, applied);
        object destroyed = Restore(lease, disabled);

        AssertState(disabled, 1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        AssertState(destroyed, 1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
    }

    [Test]
    public void Pooled_rebind_releases_the_old_baseline_before_capturing_the_new_one()
    {
        object lease = CreateLease();
        object rowA = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        object rowAApplied = Apply(lease, rowA, 110f);
        object rowARestored = Restore(lease, rowAApplied);
        object rowB = State(7f, 8f, 9f, 10f, 12, 13);

        object rowBApplied = Apply(lease, rowB, 40f);
        object rowBRestored = Restore(lease, rowBApplied);

        AssertState(rowARestored, 1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        AssertState(rowBApplied, 7f, 8f, 49f, 10f, NoWrap, Ellipsis);
        AssertState(rowBRestored, 7f, 8f, 9f, 10f, 12, 13);
    }

    [Test]
    public void Restore_preserves_each_field_taken_over_by_an_external_owner()
    {
        object lease = CreateLease();
        object baseline = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        object applied = Apply(lease, baseline, 110f);
        object externallyChanged = State(9f, 2f, 113f, 4f, NoWrap, 17);

        object restored = Restore(lease, externallyChanged);

        AssertState(restored, 9f, 2f, 3f, 4f, BaselineWrap, 17);
    }

    [Test]
    public void Refresh_adopts_external_changes_as_the_next_restore_baseline()
    {
        object lease = CreateLease();
        object baseline = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        Apply(lease, baseline, 110f);
        object externallyChanged = State(9f, 2f, 113f, 4f, NoWrap, 17);

        object refreshed = Apply(lease, externallyChanged, 90f);
        object restored = Restore(lease, refreshed);

        AssertState(refreshed, 9f, 2f, 93f, 4f, NoWrap, Ellipsis);
        AssertState(restored, 9f, 2f, 3f, 4f, BaselineWrap, 17);
    }

    [Test]
    public void Applied_state_is_current_until_an_external_field_changes()
    {
        object lease = CreateLease();
        object baseline = State(1f, 2f, 3f, 4f, BaselineWrap, BaselineOverflow);
        Assert.That(Read<bool>(lease, "HasActiveLease"), Is.False);
        object applied = Apply(lease, baseline, 110f);
        object externallyChanged = State(1f, 2f, 114f, 4f, NoWrap, Ellipsis);

        Assert.Multiple(() =>
        {
            Assert.That(Read<bool>(lease, "HasActiveLease"), Is.True);
            Assert.That(MatchesApplied(lease, applied), Is.True);
            Assert.That(MatchesApplied(lease, externallyChanged), Is.False);
        });

        Restore(lease, applied);
        Assert.That(Read<bool>(lease, "HasActiveLease"), Is.False);
    }

    private static object CreateLease()
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeNativeTextPresentationLease");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeNativeTextPresentationLease production type is missing.");
        }

        return Activator.CreateInstance(type!, nonPublic: true)!;
    }

    private static object State(
        float left,
        float top,
        float right,
        float bottom,
        int wrappingMode,
        int overflowMode)
    {
        Type? type = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.PlayerBadgeNativeTextPresentation");
        if (type == null)
        {
            Assert.Fail("PlayerBadgeNativeTextPresentation production type is missing.");
        }

        return Activator.CreateInstance(
            type!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[] { left, top, right, bottom, wrappingMode, overflowMode },
            culture: null)!;
    }

    private static object Apply(object lease, object current, float reservation)
    {
        return Invoke(lease, "Apply", current, reservation, NoWrap, Ellipsis);
    }

    private static object Restore(object lease, object current)
    {
        return Invoke(lease, "Restore", current);
    }

    private static bool MatchesApplied(object lease, object current)
    {
        return (bool)Invoke(lease, "MatchesApplied", current);
    }

    private static object Invoke(object target, string methodName, params object[] arguments)
    {
        MethodInfo? method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            Assert.Fail($"Lease method {methodName} is missing.");
        }

        return method!.Invoke(target, arguments)!;
    }

    private static void AssertState(
        object state,
        float left,
        float top,
        float right,
        float bottom,
        int wrappingMode,
        int overflowMode)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Read<float>(state, "MarginLeft"), Is.EqualTo(left));
            Assert.That(Read<float>(state, "MarginTop"), Is.EqualTo(top));
            Assert.That(Read<float>(state, "MarginRight"), Is.EqualTo(right));
            Assert.That(Read<float>(state, "MarginBottom"), Is.EqualTo(bottom));
            Assert.That(Read<int>(state, "WrappingMode"), Is.EqualTo(wrappingMode));
            Assert.That(Read<int>(state, "OverflowMode"), Is.EqualTo(overflowMode));
        });
    }

    private static T Read<T>(object value, string propertyName)
    {
        PropertyInfo? property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            Assert.Fail($"Presentation property {propertyName} is missing.");
        }

        return (T)property!.GetValue(value)!;
    }
}
