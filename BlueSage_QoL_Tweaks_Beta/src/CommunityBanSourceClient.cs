using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using BlueSage.QoLTweaks.Core;
using UnityEngine.Networking;

namespace BlueSage.QoLTweaks;

internal sealed class CommunityBanSourceResult
{
    internal bool Succeeded { get; set; }
    internal string Payload { get; set; } = string.Empty;
    internal string HttpResult { get; set; } = "not attempted";
    internal string SourceRevision { get; set; } = "unavailable";
    internal string SourceHash { get; set; } = "unavailable";
    internal string Error { get; set; } = string.Empty;
}

internal static class CommunityBanSourceClient
{
    internal const int RequestTimeoutSeconds = 10;
    internal const string SourceUrl =
        "https://raw.githubusercontent.com/andrewlimforfun/ot-community-banlist/master/BanData.txt";

    internal static IEnumerator Fetch(Action<CommunityBanSourceResult> completed)
    {
        CommunityBanSourceResult result = new CommunityBanSourceResult();
        using (UnityWebRequest request = UnityWebRequest.Get(SourceUrl))
        {
            request.timeout = RequestTimeoutSeconds;
            yield return request.SendWebRequest();

            long statusCode = request.responseCode;
            result.HttpResult = statusCode > 0
                ? statusCode.ToString() + " " + request.result
                : request.result.ToString();
            if (request.result != UnityWebRequest.Result.Success ||
                statusCode < 200 ||
                statusCode >= 300)
            {
                result.Error = CommunityBanSyncPolicy.BoundError(
                    "fetch failed: " + request.result + " (HTTP " + statusCode + ")");
                completed?.Invoke(result);
                yield break;
            }

            string payload = request.downloadHandler?.text ?? string.Empty;
            int payloadBytes = Encoding.UTF8.GetByteCount(payload);
            if (payloadBytes <= 0 || payloadBytes > CommunityBanListController.MaxPayloadBytes)
            {
                result.Error = "payload size rejected";
                completed?.Invoke(result);
                yield break;
            }

            result.Payload = payload;
            result.SourceHash = ComputeSha256(payload);
            string revision = request.GetResponseHeader("ETag");
            if (string.IsNullOrWhiteSpace(revision))
            {
                revision = request.GetResponseHeader("Last-Modified");
            }
            result.SourceRevision = CommunityBanSyncPolicy.BoundStatusField(revision);
            result.Succeeded = true;
            completed?.Invoke(result);
        }
    }

    private static string ComputeSha256(string value)
    {
        using (SHA256 hash = SHA256.Create())
        {
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            StringBuilder text = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                text.Append(digest[index].ToString("X2"));
            }
            return text.ToString();
        }
    }
}
