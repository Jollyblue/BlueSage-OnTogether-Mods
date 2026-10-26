using System;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class WorkRelaxCommandIntegrationTests
{
    [TestCase("/workon", 2, 1)]
    [TestCase("/relaxoff", 2, 1)]
    [TestCase("/relaxon", 1, 2)]
    [TestCase("/workoff", 1, 2)]
    [TestCase("/mode work", 2, 1)]
    [TestCase("/mode relax", 1, 2)]
    public void RegisteredCommand_ChangesModeLocallyWithOneConciseMessage(
        string input,
        int initialValue,
        int expectedValue)
    {
        WorkRelaxMode initial = (WorkRelaxMode)initialValue;
        WorkRelaxMode expected = (WorkRelaxMode)expectedValue;
        WorkRelaxMode current = initial;
        int writes = 0;
        var registry = new BlueSageCommandRegistry();
        WorkRelaxCommandPolicy.RegisterCommands(
            registry,
            () => current,
            mode =>
            {
                writes++;
                current = mode;
                return true;
            });

        BlueSageCommandResult result = registry.TryExecute(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Handled, Is.True, "handled commands stay local instead of reaching lobby chat");
            Assert.That(current, Is.EqualTo(expected));
            Assert.That(writes, Is.EqualTo(1));
            Assert.That(result.Messages, Is.EqualTo(new[] { $"Mode: {expected}." }));
        });
    }

    [TestCase("/workon", 1)]
    [TestCase("/relaxoff", 1)]
    [TestCase("/relaxon", 2)]
    [TestCase("/workoff", 2)]
    [TestCase("/mode work", 1)]
    [TestCase("/mode relax", 2)]
    public void RegisteredCommand_WhenAlreadySelected_IsHandledWithoutWriteAndSaysSo(
        string input,
        int initialValue)
    {
        WorkRelaxMode initial = (WorkRelaxMode)initialValue;
        int writes = 0;
        var registry = new BlueSageCommandRegistry();
        WorkRelaxCommandPolicy.RegisterCommands(
            registry,
            () => initial,
            _ =>
            {
                writes++;
                return true;
            });

        BlueSageCommandResult result = registry.TryExecute(input);

        Assert.Multiple(() =>
        {
            Assert.That(result.Handled, Is.True, "idempotent commands are still consumed locally");
            Assert.That(writes, Is.Zero);
            Assert.That(result.Messages, Is.EqualTo(new[] { $"You're already in {initial} mode." }));
        });
    }

    [Test]
    public void EarlyWorkCommand_WhenSavedRelaxLoadsDuringInitialization_AppliesRequestedWork()
    {
        WorkRelaxMode? current = null;
        int writes = 0;
        var registry = new BlueSageCommandRegistry();
        WorkRelaxCommandPolicy.RegisterCommands(
            registry,
            () => current,
            requested =>
            {
                WorkRelaxMode restoredFromLedger = WorkRelaxMode.Relax;
                current = restoredFromLedger;
                writes++;
                current = requested;
                return true;
            });

        BlueSageCommandResult result = registry.TryExecute("/workon");

        Assert.Multiple(() =>
        {
            Assert.That(result.Handled, Is.True);
            Assert.That(writes, Is.EqualTo(1), "unavailable mode must attempt initialization instead of assuming Work");
            Assert.That(current, Is.EqualTo(WorkRelaxMode.Work));
            Assert.That(result.Messages, Is.EqualTo(new[] { "Mode: Work." }));
        });
    }

    [Test]
    public void EarlyWorkCommand_WhenRuntimeCannotInitialize_ReturnsOneUnavailableMessage()
    {
        var registry = new BlueSageCommandRegistry();
        WorkRelaxCommandPolicy.RegisterCommands(
            registry,
            () => null,
            _ => false);

        BlueSageCommandResult result = registry.TryExecute("/relaxoff");

        Assert.Multiple(() =>
        {
            Assert.That(result.Handled, Is.True);
            Assert.That(result.Messages, Is.EqualTo(new[] { "Work/Relax isn't ready yet. Try again in a few seconds." }));
        });
    }
}
