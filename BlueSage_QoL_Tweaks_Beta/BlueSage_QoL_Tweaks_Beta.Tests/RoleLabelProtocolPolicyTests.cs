using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class RoleLabelProtocolPolicyTests
{
    private const long Now = 1_784_395_200;
    private const string Nonce = "Ab12Cd34";
    private const string Label = "<color=#70D6FF><b>♪ Host ♪</b></color>";

    [Test]
    public void Set_payload_round_trips_current_bounded_balanced_rich_text()
    {
        Assert.That(RoleLabelProtocolPolicy.TryBuildSet(Label, Now, Nonce, out string payload, out string buildError), Is.True, buildError);
        Assert.That(RoleLabelProtocolPolicy.TryParse(payload, Now + 1, out RoleLabelProtocolMessage message, out string parseError), Is.True, parseError);
        Assert.Multiple(() =>
        {
            Assert.That(message.Action, Is.EqualTo(RoleLabelProtocolAction.Set));
            Assert.That(message.IssuedUnixSeconds, Is.EqualTo(Now));
            Assert.That(message.Nonce, Is.EqualTo(Nonce));
            Assert.That(message.Label, Is.EqualTo(Label));
            Assert.That(payload.Split('|').Length, Is.EqualTo(5));
            Assert.That(payload, Does.Not.Contain(Label));
        });
    }

    [Test]
    public void Payload_rejects_wrong_version_action_bad_base64_bad_nonce_empty_or_overlength_label()
    {
        Assert.That(RoleLabelProtocolPolicy.TryBuildSet(Label, Now, Nonce, out string valid, out _), Is.True);
        string encodedEmpty = string.Empty;
        string encodedMalformedRichText = Base64Url("<b>Host");

        Assert.Multiple(() =>
        {
            Assert.That(Parse(valid.Replace("v1|", "v2|", StringComparison.Ordinal)), Is.False);
            Assert.That(Parse(valid.Replace("|set|", "|rename|", StringComparison.Ordinal)), Is.False);
            Assert.That(Parse($"v1|set|{Now}|{Nonce}|***"), Is.False);
            Assert.That(Parse($"v1|set|{Now}|bad-|{Base64Url(Label)}"), Is.False);
            Assert.That(Parse($"v1|set|{Now}|{Nonce}|{encodedEmpty}"), Is.False);
            Assert.That(Parse($"v1|set|{Now}|{Nonce}|{encodedMalformedRichText}"), Is.False);
            Assert.That(RoleLabelProtocolPolicy.TryBuildSet(new string('x', 97), Now, Nonce, out _, out _), Is.False);
        });
    }

    [Test]
    public void Payload_rejects_stale_or_implausibly_future_timestamp()
    {
        Assert.That(RoleLabelProtocolPolicy.TryBuildSet(Label, Now, Nonce, out string payload, out _), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(RoleLabelProtocolPolicy.TryParse(payload, Now + 9, out _, out _), Is.False);
            Assert.That(RoleLabelProtocolPolicy.TryParse(payload, Now - 3, out _, out _), Is.False);
            Assert.That(RoleLabelProtocolPolicy.TryParse(payload, Now + 8, out _, out _), Is.True);
            Assert.That(RoleLabelProtocolPolicy.TryParse(payload, Now - 2, out _, out _), Is.True);
        });
    }

    [Test]
    public void Clear_payload_is_valid_and_contains_no_label()
    {
        Assert.That(RoleLabelProtocolPolicy.TryBuildClear(Now, Nonce, out string payload, out string buildError), Is.True, buildError);
        Assert.That(RoleLabelProtocolPolicy.TryParse(payload, Now, out RoleLabelProtocolMessage message, out string parseError), Is.True, parseError);
        Assert.Multiple(() =>
        {
            Assert.That(message.Action, Is.EqualTo(RoleLabelProtocolAction.Clear));
            Assert.That(message.Label, Is.Empty);
            Assert.That(payload, Does.EndWith("|-"));
            Assert.That(payload, Does.Not.Contain("Host"));
        });
    }

    private static bool Parse(string payload) =>
        RoleLabelProtocolPolicy.TryParse(payload, Now, out _, out _);

    private static string Base64Url(string value) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
