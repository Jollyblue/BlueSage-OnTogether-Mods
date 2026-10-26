using System;
using System.Collections;
using System.IO;
using System.Text;
using BepInEx;
using BlueSage.QoLTweaks.Core;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal static class CommunityBanListController
{
    internal const float SuccessIntervalSeconds = CommunityBanSyncPolicy.SuccessIntervalSeconds;
    internal const float RetryIntervalSeconds = CommunityBanSyncPolicy.RetryIntervalSeconds;
    internal const int MaxPayloadBytes = 262144;
    private static readonly CommunityBanSyncStatus CurrentStatus = new CommunityBanSyncStatus();

    [Serializable]
    private sealed class CommunityBanPayload
    {
        public string[] BanServerPlayers = Array.Empty<string>();
        public string[] BanServerPlayerNicks = Array.Empty<string>();
    }

    internal static bool RefreshInFlight { get; private set; }

    internal static string StatusText()
    {
        CurrentStatus.Enabled = CanApply();
        return CommunityBanSyncPolicy.BuildStatusText(CurrentStatus);
    }

    internal static string FriendlyStatusText()
    {
        CurrentStatus.Enabled = CanApply();
        return CommunityBanSyncPolicy.BuildFriendlyStatusText(CurrentStatus);
    }

    internal static void SetNextAttempt(DateTime nextAttemptUtc)
    {
        CurrentStatus.Enabled = CanApply();
        CurrentStatus.NextAttemptUtc = nextAttemptUtc.ToUniversalTime();
        WriteCurrentAttestation();
    }

    internal static void MarkDisabled()
    {
        CurrentStatus.Enabled = false;
        CurrentStatus.NextAttemptUtc = null;
        WriteCurrentAttestation();
    }

    internal static IEnumerator ReconcileFromSource(Action<bool> completed)
    {
        bool succeeded = false;
        if (RefreshInFlight)
        {
            completed?.Invoke(false);
            yield break;
        }

        RefreshInFlight = true;
        DateTime attemptUtc = DateTime.UtcNow;
        CommunityBanSyncPolicy.BeginAttempt(CurrentStatus, CanApply(), attemptUtc);
        try
        {
            if (!CanApply())
            {
                Fail("sync is disabled", attemptUtc);
                yield break;
            }

            CommunityBanSourceResult source = null;
            yield return CommunityBanSourceClient.Fetch(result => source = result);
            if (source == null)
            {
                Fail("source client returned no result", attemptUtc);
                yield break;
            }

            CurrentStatus.HttpResult = CommunityBanSyncPolicy.BoundStatusField(
                source.HttpResult,
                "not attempted");
            CurrentStatus.SourceRevision = CommunityBanSyncPolicy.BoundStatusField(source.SourceRevision);
            CurrentStatus.SourceHash = source.SourceHash;
            if (!source.Succeeded)
            {
                Fail(source.Error, attemptUtc);
                yield break;
            }

            if (!TryParsePayload(source.Payload, out CommunityBanPayload payload, out string parseError))
            {
                Fail("JSON validation failed: " + parseError, attemptUtc);
                yield break;
            }

            DataManager data = MonoSingleton<DataManager>.I;
            BanData bans = data?.BanData;
            if (bans?.BanServerPlayers == null || bans.BanServerPlayerNicks == null)
            {
                Fail("native paired storage is unavailable", attemptUtc);
                yield break;
            }

            if (!CommunityBanListMergePolicy.TryBuildAdditions(
                    bans.BanServerPlayers,
                    bans.BanServerPlayerNicks,
                    payload?.BanServerPlayers,
                    payload?.BanServerPlayerNicks,
                    out CommunityBanAddition[] additions,
                    out string validationError))
            {
                Fail(validationError, attemptUtc);
                yield break;
            }

            if (!NativeBanStoreAdapter.TryApplyAdditions(
                    additions,
                    out int existingCount,
                    out string persistError))
            {
                Fail("native persistence failed: " + persistError, attemptUtc);
                yield break;
            }

            int acceptedCount = payload?.BanServerPlayers?.Length ?? 0;
            CurrentStatus.AcceptedCount = acceptedCount;
            CurrentStatus.LastSuccessUtc = DateTime.UtcNow;
            CurrentStatus.NextAttemptUtc = CommunityBanSyncPolicy.ComputeNextAttemptUtc(
                CurrentStatus.LastSuccessUtc.Value,
                succeeded: true);
            CurrentStatus.BoundedError = string.Empty;
            WriteCurrentAttestation();
            Plugin.Log?.LogInfo(
                "[BlueSageCommunityBanList] current upstream validated; accepted=" +
                acceptedCount +
                "; existingPreserved=" +
                existingCount +
                "; addedMissing=" +
                additions.Length +
                "; liveActions=0; authorityGrants=0.");
            succeeded = true;
        }
        finally
        {
            RefreshInFlight = false;
            completed?.Invoke(succeeded);
        }
    }

    internal static void WriteCurrentAttestation()
    {
        try
        {
            // Round 5d: BlueSageQoLModSuite/trackers/community-ban-attestation.json.
            string target = ModSuitePaths.Current.AttestationPath;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temp = target + ".tmp";
            string backup = target + ".bak";
            File.WriteAllText(
                temp,
                CommunityBanSyncPolicy.BuildAttestationJson(CurrentStatus),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(target))
            {
                File.Replace(temp, target, backup, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, target);
            }
        }
        catch (Exception ex)
        {
            CurrentStatus.BoundedError = CommunityBanSyncPolicy.BoundError(
                string.IsNullOrWhiteSpace(CurrentStatus.BoundedError)
                    ? "attestation write failed: " + ex.GetType().Name
                    : CurrentStatus.BoundedError + "; attestation write failed: " + ex.GetType().Name);
            Plugin.Log?.LogWarning(
                "[BlueSageCommunityBanList] bounded attestation write failed: " +
                ex.GetType().Name +
                ". No identity fields were written.");
        }
    }

    private static void Fail(string error, DateTime attemptUtc)
    {
        CurrentStatus.BoundedError = CommunityBanSyncPolicy.BoundError(error);
        CurrentStatus.NextAttemptUtc = CommunityBanSyncPolicy.ComputeNextAttemptUtc(
            attemptUtc,
            succeeded: false);
        WriteCurrentAttestation();
        Plugin.Log?.LogWarning(
            "[BlueSageCommunityBanList] refresh rejected safely: " +
            (CurrentStatus.BoundedError.Length == 0 ? "unavailable" : CurrentStatus.BoundedError) +
            ". Local native ban pairs were not intentionally changed.");
    }

    private static bool CanApply()
    {
        return Plugin.EnableCommunityBanListSync != null &&
               Plugin.EnableCommunityBanListSync.Value;
    }

    private static bool TryParsePayload(
        string json,
        out CommunityBanPayload payload,
        out string error)
    {
        payload = null;
        error = string.Empty;
        try
        {
            payload = JsonUtility.FromJson<CommunityBanPayload>(json ?? string.Empty);
            if (payload != null) return true;
            error = "empty document";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.GetType().Name;
            return false;
        }
    }
}
