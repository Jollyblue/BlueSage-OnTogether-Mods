using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CommunityBanListMergePolicyTests
{
    private const string Existing = "76561198000000001";
    private const string Missing = "76561198000000002";
    private const string SecondMissing = "76561198000000003";

    [Test]
    public void BuildAdditionsPreservesLocalRowsAndReturnsOnlyMissingRemoteIds()
    {
        bool ok = CommunityBanListMergePolicy.TryBuildAdditions(
            new[] { Existing },
            new[] { "Blue local reason" },
            new[] { Existing, Missing },
            new[] { "Upstream replacement attempt", "Community reason" },
            out CommunityBanAddition[] additions,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, error);
            Assert.That(additions, Has.Length.EqualTo(1));
            Assert.That(additions[0].SteamId, Is.EqualTo(Missing));
            Assert.That(additions[0].Nickname, Is.EqualTo("Community reason"));
        });
    }

    [Test]
    public void BuildAdditionsRejectsEntireSourceWhenAnyRemoteSteamIdIsInvalid()
    {
        bool ok = CommunityBanListMergePolicy.TryBuildAdditions(
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { Missing, "bad", SecondMissing },
            new[] { "first", "bad", "second" },
            out CommunityBanAddition[] additions,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(additions, Is.Empty);
            Assert.That(error, Does.Contain("invalid SteamID64"));
        });
    }

    [Test]
    public void BuildAdditionsRejectsEntireSourceWhenRemoteSteamIdIsDuplicated()
    {
        bool ok = CommunityBanListMergePolicy.TryBuildAdditions(
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { Missing, Missing, SecondMissing },
            new[] { "first", "duplicate", "second" },
            out CommunityBanAddition[] additions,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(additions, Is.Empty);
            Assert.That(error, Does.Contain("duplicate SteamID64"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BuildAdditionsRejectsMisalignedPairedLists(bool localLists)
    {
        bool ok = CommunityBanListMergePolicy.TryBuildAdditions(
            localLists ? new[] { Existing } : Array.Empty<string>(),
            Array.Empty<string>(),
            localLists ? Array.Empty<string>() : new[] { Missing },
            Array.Empty<string>(),
            out CommunityBanAddition[] additions,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(additions, Is.Empty);
            Assert.That(error, Does.Contain("paired"));
        });
    }

    [Test]
    public void BuildAdditionsSanitizesControlCharactersAndBoundsNicknameLength()
    {
        string noisy = "  <b>community</b>\r\nreason\t\u202E" + new string('x', 100) + "  ";

        bool ok = CommunityBanListMergePolicy.TryBuildAdditions(
            Array.Empty<string>(),
            Array.Empty<string>(),
            new[] { Missing },
            new[] { noisy },
            out CommunityBanAddition[] additions,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True, error);
            Assert.That(additions, Has.Length.EqualTo(1));
            Assert.That(additions[0].Nickname, Has.Length.LessThanOrEqualTo(64));
            Assert.That(additions[0].Nickname, Does.Not.Contain("\r").And.Not.Contain("\n").And.Not.Contain("\t"));
            Assert.That(additions[0].Nickname, Does.Not.Contain("<"));
            Assert.That(additions[0].Nickname, Does.Not.Contain(">"));
            Assert.That(additions[0].Nickname.IndexOf('\u202E'), Is.EqualTo(-1));
        });
    }

    [Test]
    public void BuildAdditionsRejectsUnreasonablyLargeRemotePayloads()
    {
        string[] ids = Enumerable.Range(0, CommunityBanListMergePolicy.MaxRemoteRows + 1)
            .Select(index => (76561198000000000UL + (ulong)index).ToString())
            .ToArray();

        bool ok = CommunityBanListMergePolicy.TryBuildAdditions(
            Array.Empty<string>(),
            Array.Empty<string>(),
            ids,
            ids,
            out CommunityBanAddition[] additions,
            out string error);

        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.False);
            Assert.That(additions, Is.Empty);
            Assert.That(error, Does.Contain("limit"));
        });
    }
}
