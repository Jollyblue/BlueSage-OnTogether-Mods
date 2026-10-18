using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxCommandPolicyTests
{
    [TestCase("/workon", 1)]
    [TestCase("/relaxoff", 1)]
    [TestCase("/relaxon", 2)]
    [TestCase("/workoff", 2)]
    public void Direct_commands_select_their_literal_mode(string commandName, int expectedValue)
    {
        bool resolved = WorkRelaxCommandPolicy.TryResolve(
            commandName,
            Array.Empty<string>(),
            out WorkRelaxMode mode,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True, error);
            Assert.That(mode, Is.EqualTo((WorkRelaxMode)expectedValue));
            Assert.That(error, Is.Empty);
        });
    }

    [TestCase("work", 1)]
    [TestCase("relax", 2)]
    public void Mode_command_selects_the_requested_mode(string argument, int expectedValue)
    {
        bool resolved = WorkRelaxCommandPolicy.TryResolve(
            "/mode",
            new[] { argument },
            out WorkRelaxMode mode,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.True, error);
            Assert.That(mode, Is.EqualTo((WorkRelaxMode)expectedValue));
            Assert.That(error, Is.Empty);
        });
    }

    [Test]
    public void Mode_command_rejects_a_missing_argument()
    {
        bool resolved = WorkRelaxCommandPolicy.TryResolve(
            "/mode",
            Array.Empty<string>(),
            out _,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("work").And.Contain("relax"));
        });
    }

    [Test]
    public void Mode_command_rejects_an_unknown_argument()
    {
        bool resolved = WorkRelaxCommandPolicy.TryResolve(
            "/mode",
            new[] { "focus" },
            out _,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.False);
            Assert.That(error, Does.Contain("work").And.Contain("relax"));
        });
    }
}
