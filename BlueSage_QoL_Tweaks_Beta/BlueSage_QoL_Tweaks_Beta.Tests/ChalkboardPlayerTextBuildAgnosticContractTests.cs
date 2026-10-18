using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// 2026-10-17: player-facing chalkboard messages named "build 24259292" while the live game is
// 25966942, so players saw a stale build number. Messages must not name a game build at all. The
// internal provenance (ChalkboardCatalogPolicy.CatalogRevision = "build-24259292-v1") stays: the
// catalog really was verified on that build, and re-verifying it on 25966942 is a UAT item.
public sealed class ChalkboardPlayerTextBuildAgnosticContractTests
{
    [TestCase("Core/ChalkboardCommandPolicy.cs")]
    [TestCase("Core/ChalkboardCatalogPolicy.cs")]
    [TestCase("ChalkboardPersistenceController.cs")]
    public void Player_facing_chalkboard_strings_do_not_name_a_game_build(string file)
    {
        string source = ReadSource(file);
        MatchCollection stringLiterals = Regex.Matches(source, "\"(?:[^\"\\\\]|\\\\.)*\"");

        foreach (Match literal in stringLiterals)
        {
            if (literal.Value.StartsWith("\"build-", System.StringComparison.Ordinal))
            {
                continue; // internal catalog revision id, not player text
            }

            Assert.That(literal.Value, Does.Not.Match(@"build \d{8}"), $"{file} player text names a game build: {literal.Value}");
        }
    }

    [Test]
    public void Catalog_provenance_is_kept_internally()
    {
        Assert.That(ReadSource("Core/ChalkboardCatalogPolicy.cs"), Does.Contain("internal const string CatalogRevision = \"build-24259292-v1\";"));
    }

    private static string ReadSource(string relative)
    {
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException();
        }

        return File.ReadAllText(Path.Combine(root, "src", relative));
    }
}
