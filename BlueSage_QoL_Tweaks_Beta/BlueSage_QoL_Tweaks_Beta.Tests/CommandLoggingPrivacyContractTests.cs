using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CommandLoggingPrivacyContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Handled_command_logging_never_writes_raw_arguments()
    {
        string plugin = File.ReadAllText(Path.Combine(Root, "src", "Plugin.cs"));
        string registry = File.ReadAllText(Path.Combine(Root, "src", "Core", "BlueSageCommandRegistry.cs"));

        Assert.Multiple(() =>
        {
            Assert.That(registry, Does.Contain("internal static bool TryParse"));
            // Round 5h: the arguments are parsed only to decide whether a command also toasts (CommandToastPolicy); the log line stays redacted.
            Assert.That(plugin, Does.Contain("BlueSageCommandRegistry.TryParse(text, out string commandName, out string commandArguments)"));
            Assert.That(plugin, Does.Not.Contain("command handled locally: /\" + loggedCommand + \" \" + commandArguments"));
            Assert.That(plugin, Does.Contain("(arguments redacted)."));
            Assert.That(plugin, Does.Not.Contain("command handled locally: {text}"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? TestContext.CurrentContext.TestDirectory;
    }
}
