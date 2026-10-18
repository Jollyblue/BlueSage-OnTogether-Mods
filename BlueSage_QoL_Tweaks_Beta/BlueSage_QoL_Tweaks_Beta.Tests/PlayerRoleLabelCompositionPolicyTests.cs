using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class PlayerRoleLabelCompositionPolicyTests
{
    private const string Owner = "76561198000000001";
    private const string Helper = "76561198000000002";
    private const string Other = "76561198000000003";
    private const string HostLabel = "<color=#70D6FF><b>♪ Host ♪</b></color>";
    private const string HelperLabel = "<color=#A8D27A><b>(Helper)</b></color>";

    [Test]
    public void Host_requires_exact_current_owner_host_and_unique_row()
    {
        PlayerRoleLabelCompositionResult valid = Compose("Blue (Host)", Owner, isHost: true, unique: true);
        PlayerRoleLabelCompositionResult wrongId = Compose("Blue (Host)", Other, isHost: true, unique: true);
        PlayerRoleLabelCompositionResult notHost = Compose("Blue (Host)", Owner, isHost: false, unique: true);
        PlayerRoleLabelCompositionResult duplicate = Compose("Blue (Host)", Owner, isHost: true, unique: false);

        Assert.Multiple(() =>
        {
            Assert.That(valid.Role, Is.EqualTo(PlayerRoleLabelKind.Host));
            Assert.That(valid.RenderedText, Is.EqualTo("Blue " + HostLabel));
            Assert.That(wrongId.Applied, Is.False);
            Assert.That(notHost.Applied, Is.False);
            Assert.That(duplicate.Applied, Is.False);
        });
    }

    [Test]
    public void Host_replaces_only_exact_localized_native_suffix_and_never_duplicates()
    {
        PlayerRoleLabelCompositionResult composed = Compose("Blue (Code) (Host)", Owner, isHost: true, unique: true);
        PlayerRoleLabelCompositionResult missingExactSuffix = Compose("Blue (Host-ish)", Owner, isHost: true, unique: true);
        PlayerRoleLabelCompositionResult alreadyApplied = Compose("Blue " + HostLabel, Owner, isHost: true, unique: true);

        Assert.Multiple(() =>
        {
            Assert.That(composed.RenderedText, Is.EqualTo("Blue (Code) " + HostLabel));
            Assert.That(missingExactSuffix.Applied, Is.False);
            Assert.That(alreadyApplied.RenderedText, Is.EqualTo("Blue " + HostLabel));
            Assert.That(Count(alreadyApplied.RenderedText, HostLabel), Is.EqualTo(1));
        });
    }

    [Test]
    public void Helper_requires_fresh_capability_owner_scoped_exact_id_and_unique_row()
    {
        PlayerRoleLabelCompositionResult valid = Compose("Robin [Resting]", Helper, isHost: false, unique: true, capabilityFresh: true, helpers: new[] { Helper });
        PlayerRoleLabelCompositionResult stale = Compose("Robin [Resting]", Helper, isHost: false, unique: true, capabilityFresh: false, helpers: new[] { Helper });
        PlayerRoleLabelCompositionResult partial = Compose("Robin [Resting]", Helper, isHost: false, unique: true, capabilityFresh: true, helpers: new[] { Helper.Substring(0, Helper.Length - 1) });
        PlayerRoleLabelCompositionResult duplicate = Compose("Robin [Resting]", Helper, isHost: false, unique: false, capabilityFresh: true, helpers: new[] { Helper });

        Assert.Multiple(() =>
        {
            Assert.That(valid.Role, Is.EqualTo(PlayerRoleLabelKind.Helper));
            Assert.That(valid.RenderedText, Is.EqualTo("Robin [Resting] " + HelperLabel));
            Assert.That(stale.Applied, Is.False);
            Assert.That(partial.Applied, Is.False);
            Assert.That(duplicate.Applied, Is.False);
        });
    }

    [Test]
    public void Host_never_receives_helper_label_when_mislisted()
    {
        PlayerRoleLabelCompositionResult result = Compose("Blue (Host)", Owner, isHost: true, unique: true, capabilityFresh: true, helpers: new[] { Owner });
        Assert.Multiple(() =>
        {
            Assert.That(result.Role, Is.EqualTo(PlayerRoleLabelKind.Host));
            Assert.That(result.RenderedText, Does.Contain(HostLabel));
            Assert.That(result.RenderedText, Does.Not.Contain(HelperLabel));
        });
    }

    [Test]
    public void Helper_decoration_is_idempotent_and_restore_preserves_foreign_text()
    {
        PlayerRoleLabelCompositionResult first = Compose("Robin", Helper, isHost: false, unique: true, capabilityFresh: true, helpers: new[] { Helper });
        PlayerRoleLabelCompositionResult second = Compose(first.RenderedText, Helper, isHost: false, unique: true, capabilityFresh: true, helpers: new[] { Helper });

        Assert.That(PlayerRoleLabelCompositionPolicy.TryRestore(second.RenderedText, first.RenderedText, "Robin", out string restored), Is.True);
        Assert.That(PlayerRoleLabelCompositionPolicy.TryRestore("Foreign rewrite", first.RenderedText, "Robin", out string foreign), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(Count(second.RenderedText, HelperLabel), Is.EqualTo(1));
            Assert.That(restored, Is.EqualTo("Robin"));
            Assert.That(foreign, Is.EqualTo("Foreign rewrite"));
        });
    }

    private static PlayerRoleLabelCompositionResult Compose(
        string nativeText,
        string rowSteamId,
        bool isHost,
        bool unique,
        bool capabilityFresh = false,
        IEnumerable<string>? helpers = null) =>
        PlayerRoleLabelCompositionPolicy.Compose(
            nativeText,
            "(Host)",
            HostLabel,
            HelperLabel,
            rowSteamId,
            Owner,
            isHost,
            unique,
            capabilityFresh,
            helpers ?? Array.Empty<string>());

    private static int Count(string value, string part) =>
        (value.Length - value.Replace(part, string.Empty, StringComparison.Ordinal).Length) / part.Length;
}
