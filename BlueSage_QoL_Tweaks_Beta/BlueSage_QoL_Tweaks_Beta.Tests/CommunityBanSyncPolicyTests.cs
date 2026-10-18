using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CommunityBanSyncPolicyTests
{
    [Test]
    public void Status_and_attestation_expose_only_bounded_operational_fields()
    {
        CommunityBanSyncStatus status = new()
        {
            Enabled = true,
            LastAttemptUtc = new DateTime(2026, 7, 20, 2, 0, 0, DateTimeKind.Utc),
            LastSuccessUtc = new DateTime(2026, 7, 20, 2, 0, 1, DateTimeKind.Utc),
            HttpResult = "200 Success\r\n<unsafe>",
            SourceRevision = "\"revision-1\"",
            SourceHash = new string('A', 64),
            AcceptedCount = 12,
            NextAttemptUtc = new DateTime(2026, 7, 20, 8, 0, 1, DateTimeKind.Utc),
            BoundedError = new string('x', 300)
        };

        string text = CommunityBanSyncPolicy.BuildStatusText(status);
        string attestation = CommunityBanSyncPolicy.BuildAttestationJson(status);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("enabled=yes"));
            Assert.That(text, Does.Contain("accepted=12"));
            Assert.That(text, Does.Contain("refresh never kicks, bans, or grants authority"));
            Assert.That(text, Does.Not.Contain("\r").And.Not.Contain("\n").And.Not.Contain("<").And.Not.Contain(">"));
            Assert.That(attestation, Does.Contain("\"schema\": \"bluesage-community-ban-attestation-v1\""));
            Assert.That(attestation, Does.Contain("\"acceptedCount\": 12"));
            Assert.That(attestation, Does.Not.Contain("Steam").And.Not.Contain("nickname").And.Not.Contain("player"));
            Assert.That(attestation.Length, Is.LessThan(1024));
        });
    }

    [Test]
    public void Invalid_hash_and_control_heavy_fields_fail_to_safe_bounded_values()
    {
        CommunityBanSyncStatus status = new()
        {
            HttpResult = "\r\n",
            SourceRevision = new string('r', 400),
            SourceHash = "not-a-hash",
            BoundedError = "\u0000" + new string('e', 400)
        };

        string text = CommunityBanSyncPolicy.BuildStatusText(status);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("http=not attempted"));
            Assert.That(text, Does.Contain("sha256=unavailable"));
            Assert.That(CommunityBanSyncPolicy.BoundStatusField(status.SourceRevision), Has.Length.EqualTo(128));
            Assert.That(CommunityBanSyncPolicy.BoundError(status.BoundedError), Has.Length.EqualTo(192));
        });
    }

    [Test]
    public void Beginning_an_attempt_clears_attempt_scoped_source_fields_without_erasing_last_success()
    {
        DateTime priorSuccess = new DateTime(2026, 7, 20, 1, 0, 0, DateTimeKind.Utc);
        DateTime attempt = new DateTime(2026, 7, 20, 2, 0, 0, DateTimeKind.Utc);
        CommunityBanSyncStatus status = new()
        {
            LastSuccessUtc = priorSuccess,
            HttpResult = "200 Success",
            SourceRevision = "old-revision",
            SourceHash = new string('A', 64),
            AcceptedCount = 42,
            NextAttemptUtc = attempt.AddHours(6),
            BoundedError = "old error"
        };

        CommunityBanSyncPolicy.BeginAttempt(status, enabled: true, attempt);

        Assert.Multiple(() =>
        {
            Assert.That(status.Enabled, Is.True);
            Assert.That(status.LastAttemptUtc, Is.EqualTo(attempt));
            Assert.That(status.LastSuccessUtc, Is.EqualTo(priorSuccess));
            Assert.That(status.HttpResult, Is.EqualTo("not attempted"));
            Assert.That(status.SourceRevision, Is.EqualTo("unavailable"));
            Assert.That(status.SourceHash, Is.EqualTo("unavailable"));
            Assert.That(status.AcceptedCount, Is.Zero);
            Assert.That(status.NextAttemptUtc, Is.Null);
            Assert.That(status.BoundedError, Is.Empty);
        });
    }

    [Test]
    public void Friendly_status_explains_local_safety_without_exposing_transport_or_hash_noise()
    {
        CommunityBanSyncStatus ready = new()
        {
            Enabled = true,
            LastSuccessUtc = new DateTime(2026, 7, 20, 2, 0, 1, DateTimeKind.Utc),
            HttpResult = "200 Success",
            SourceRevision = "opaque-revision",
            SourceHash = new string('A', 64),
            AcceptedCount = 18
        };
        CommunityBanSyncStatus failed = new()
        {
            Enabled = true,
            LastAttemptUtc = new DateTime(2026, 7, 20, 3, 0, 1, DateTimeKind.Utc),
            BoundedError = "network unavailable"
        };

        string friendlyReady = CommunityBanSyncPolicy.BuildFriendlyStatusText(ready);
        string friendlyFailed = CommunityBanSyncPolicy.BuildFriendlyStatusText(failed);

        Assert.Multiple(() =>
        {
            Assert.That(friendlyReady, Is.EqualTo("Community safety list ready: 18 trusted entries checked locally. No player action was taken."));
            Assert.That(friendlyReady, Does.Not.Contain("http").And.Not.Contain("sha256").And.Not.Contain("revision"));
            Assert.That(friendlyFailed, Is.EqualTo("Community safety list could not refresh safely. Your existing local list was left unchanged."));
            Assert.That(CommunityBanSyncPolicy.BuildFriendlyStatusText(new CommunityBanSyncStatus()), Is.EqualTo("Community safety list is off."));
        });
    }
}
