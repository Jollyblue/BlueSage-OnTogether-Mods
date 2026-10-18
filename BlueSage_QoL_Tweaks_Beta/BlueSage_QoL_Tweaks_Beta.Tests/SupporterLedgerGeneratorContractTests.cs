using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed partial class SupporterLedgerGeneratorContractTests
{
    private static readonly string Root = FindProjectRoot();

    [Test]
    public void Generated_source_contains_nineteen_transactions_and_no_stale_identity()
    {
        string source = File.ReadAllText(Path.Combine(Root, "src", "Data", "SupporterLedger.g.cs"));

        Assert.Multiple(() =>
        {
            // 2026-10-17 (Blue): Eirian (2026-09-16) and a second Lexiaphantasia gift (2026-10-16) added.
            Assert.That(source.Split("new SupporterTransaction(", System.StringSplitOptions.None).Length - 1, Is.EqualTo(21));
            Assert.That(source, Does.Contain("new SupporterTransaction(\"2026-09-16\", \"Eirian\", \"Thank you for the OT mod and lobby, and happy birthday!"));
            Assert.That(source, Does.Contain("new SupporterTransaction(\"2026-10-16\", \"Lexiaphantasia\", \"Get Rowan-n-company extra birthday treats :3\", \"30.00\")"));
            Assert.That(source, Does.Contain("Lexiaphantasia"));
            Assert.That(source, Does.Contain("Neonicalle"));
            Assert.That(source, Does.Contain("Bochi"));
            Assert.That(source, Does.Contain("new SupporterTransaction(\"2026-07-25 15:31 UTC\", \"Neonicalle\""));
            Assert.That(source, Does.Contain("new SupporterTransaction(\"2026-07-25 19:32 UTC\", \"Bochi\""));
            Assert.That(source, Does.Not.Contain("Lycoris"));
        });
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
