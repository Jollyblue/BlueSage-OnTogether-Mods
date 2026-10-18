using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class BlueSageCommandRegistryTests
{
    [Test]
    public void TryExecute_IgnoresNormalChat()
    {
        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("bsqol", "help", new[] { "qol" }, _ => BlueSageCommandResult.HandledWithMessages("handled")));

        BlueSageCommandResult result = registry.TryExecute("hello room");

        Assert.That(result.Handled, Is.False);
    }

    [Test]
    public void TryExecute_DispatchesAliasWithArguments()
    {
        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("bsqol", "help", new[] { "qol" }, context =>
            BlueSageCommandResult.HandledWithMessages("args=" + context.Arguments)));

        BlueSageCommandResult result = registry.TryExecute("/qol reconnect off");

        Assert.That(result.Handled, Is.True);
        Assert.That(result.Messages, Is.EquivalentTo(new[] { "args=reconnect off" }));
    }

    [Test]
    public void TryExecute_DispatchesDotSlashAlias()
    {
        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("assetsweep", "sweep", new[] { "as" }, context =>
            BlueSageCommandResult.HandledWithMessages("args=" + context.Arguments)));

        BlueSageCommandResult result = registry.TryExecute("./as off");

        Assert.That(result.Handled, Is.True);
        Assert.That(result.Messages, Is.EquivalentTo(new[] { "args=off" }));
    }

    [Test]
    public void TryExecute_IgnoresUnknownSlashCommands()
    {
        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("bsqol", "help", new[] { "qol" }, _ => BlueSageCommandResult.HandledWithMessages("handled")));

        BlueSageCommandResult result = registry.TryExecute("/dance");

        Assert.That(result.Handled, Is.False);
    }

    [Test]
    public void Suggest_ReturnsBlueSageHelpLinesForCommandSuggestions()
    {
        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("bsqol", "BlueSage QoL help", new[] { "qol" }, _ => BlueSageCommandResult.HandledWithMessages("handled")));

        IReadOnlyList<string> suggestions = registry.Suggest("/bs", 5);

        Assert.That(suggestions, Is.EquivalentTo(new[] { "/bsqol - BlueSage QoL help" }));
    }

    [Test]
    public void Suggest_DoesNotExposeHiddenCommands()
    {
        var registry = new BlueSageCommandRegistry();
        registry.Register(new BlueSageCommand("public", "shown", Array.Empty<string>(), _ => BlueSageCommandResult.HandledWithMessages("shown")));
        registry.Register(new BlueSageCommand("private", "hidden", Array.Empty<string>(), _ => BlueSageCommandResult.HandledWithMessages("secret"), isHidden: true));

        IReadOnlyList<string> suggestions = registry.Suggest("/p", 10);
        BlueSageCommandResult hiddenResult = registry.TryExecute("/private");

        Assert.That(suggestions, Is.EquivalentTo(new[] { "/public - shown" }));
        Assert.That(hiddenResult.Handled, Is.True);
        Assert.That(hiddenResult.Messages, Is.EquivalentTo(new[] { "secret" }));
    }
}
