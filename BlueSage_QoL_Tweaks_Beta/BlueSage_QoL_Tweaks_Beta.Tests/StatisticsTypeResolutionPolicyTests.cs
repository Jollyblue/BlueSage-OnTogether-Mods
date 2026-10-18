using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class StatisticsTypeResolutionPolicyTests
{
    private static readonly HashSet<string> ExactGlobalTypes = new(StringComparer.Ordinal)
    {
        "StatisticsData",
        "DataManager",
        "AchievementManager",
        "Achievement"
    };

    [Test]
    public void Exact_assembly_and_all_global_types_are_accepted()
    {
        Assert.That(
            StatisticsTypeResolutionPolicy.IsExactGameSurface(
                "Assembly-CSharp",
                name => ExactGlobalTypes.Contains(name)),
            Is.True);
    }

    [Test]
    public void Current_public_surface_without_legacy_statistics_manager_is_accepted()
    {
        Assert.That(
            StatisticsTypeResolutionPolicy.IsExactGameSurface(
                "Assembly-CSharp",
                name => ExactGlobalTypes.Contains(name)),
            Is.True);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("assembly-csharp")]
    [TestCase("PurrNet.Runtime")]
    public void Wrong_or_missing_assembly_fails_open(string? assemblySimpleName)
    {
        Assert.That(
            StatisticsTypeResolutionPolicy.IsExactGameSurface(
                assemblySimpleName,
                name => ExactGlobalTypes.Contains(name)),
            Is.False);
    }

    [TestCase("StatisticsData")]
    [TestCase("DataManager")]
    [TestCase("AchievementManager")]
    [TestCase("Achievement")]
    public void Any_missing_required_global_type_fails_open(string missingType)
    {
        Assert.That(
            StatisticsTypeResolutionPolicy.IsExactGameSurface(
                "Assembly-CSharp",
                name => !string.Equals(name, missingType, StringComparison.Ordinal) &&
                        ExactGlobalTypes.Contains(name)),
            Is.False);
    }

    [Test]
    public void Purrnet_qualified_statistics_manager_cannot_satisfy_global_surface()
    {
        Assert.That(
            StatisticsTypeResolutionPolicy.IsExactGameSurface(
                "PurrNet.Runtime",
                name => string.Equals(name, "PurrNet.StatisticsManager", StringComparison.Ordinal)),
            Is.False);
    }

    [Test]
    public void Missing_type_probe_fails_open()
    {
        Assert.That(
            StatisticsTypeResolutionPolicy.IsExactGameSurface("Assembly-CSharp", null!),
            Is.False);
    }

    [Test]
    public void Type_probe_exception_fails_open()
    {
        bool accepted = true;
        Assert.DoesNotThrow(() =>
            accepted = StatisticsTypeResolutionPolicy.IsExactGameSurface(
                "Assembly-CSharp",
                _ => throw new TypeLoadException("controlled test failure")));
        Assert.That(accepted, Is.False);
    }
}
