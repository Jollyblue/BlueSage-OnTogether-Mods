using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class CloneShieldController : MonoBehaviour
{
    private const float ScanIntervalSeconds = 6f;
    private const float WarningCooldownSeconds = 300f;
    private static readonly FieldInfo CustomizationDataField = AccessTools.Field(typeof(PlayerCustomizationController), "_customizationData");
    private readonly Dictionary<string, float> _lastWarningTimes = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, RosterCloneState> _previousRoster = new Dictionary<string, RosterCloneState>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _missingIncidentScans = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nonMatchingIncidentScans = new Dictionary<string, int>(StringComparer.Ordinal);
    private float _nextScanAt;
    private float _nextAlignmentWaitLogAt;
    private bool _alignmentWaitWasLogged;
    private string _observedLobbyCode = string.Empty;
    private bool _wasEnabled;

    private sealed class RosterCloneState
    {
        public string SteamId = string.Empty;
        public string RichName = string.Empty;
        public string PlainName = string.Empty;
        public string AvatarFingerprint = string.Empty;
    }

    private void Update()
    {
        if (!Plugin.ShouldApplyCloneShield)
        {
            if (_wasEnabled || _observedLobbyCode.Length > 0 || _previousRoster.Count > 0)
            {
                ResetObservationState();
                Patches.PlayerIdentityRoleHighlightController.RefreshAll();
            }

            _wasEnabled = false;
            return;
        }

        _wasEnabled = true;
        if (Time.unscaledTime < _nextScanAt)
        {
            return;
        }

        _nextScanAt = Time.unscaledTime + ScanIntervalSeconds;
        ScanVisibleRosterForClone();
    }

    private void ScanVisibleRosterForClone()
    {
        try
        {
            if (!TryGetActiveLobbyCode(out string lobbyCode))
            {
                ResetObservationState();
                return;
            }

            if (!string.Equals(_observedLobbyCode, lobbyCode, StringComparison.Ordinal))
            {
                ResetObservationState();
                _observedLobbyCode = lobbyCode;
                CloneIncidentLedger.Reset(lobbyCode);
            }

            CloneIncidentActionController.PollHostResolutionMetadata();

            PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
            if (panel == null)
            {
                return;
            }

            int idInfoCount = panel.IDInfos?.Count ?? 0;
            int transformCount = panel.PlayerTransforms?.Count ?? 0;
            int steamIdCount = panel.PlayerSteamIDs?.Count ?? 0;
            int playerIdCount = panel.PlayerIDs?.Count ?? 0;
            int count = Math.Max(steamIdCount, Math.Max(idInfoCount, transformCount));
            if (count == 0)
            {
                return;
            }

            // Clone evidence receives a moderation incident ID only when the
            // authoritative PurrNet/Steam roster is aligned and unique. Transient
            // join-list drift waits for the next bounded scan rather than guessing.
            IReadOnlyList<string> verifiedSteamIds = PlayerIdentityEvidenceController.GetVerifiedCurrentSteamIds(out string verificationError, out bool coverageComplete);
            bool authoritativeRosterAligned = verifiedSteamIds.Count > 0 && coverageComplete;
            if (!authoritativeRosterAligned)
            {
                LogAlignmentWait(verificationError);
                return;
            }
            if (_alignmentWaitWasLogged)
                Plugin.Log?.LogDebug("Clone Shield roster alignment recovered; moderation-grade incident observation resumed.");
            _alignmentWaitWasLogged = false;
            _nextAlignmentWaitLogAt = 0f;

            string localSteamId = GetLocalSteamId();
            var verifiedSet = new HashSet<string>(verifiedSteamIds, StringComparer.Ordinal);
            var currentRoster = new Dictionary<string, RosterCloneState>(StringComparer.Ordinal);
            for (int index = 0; index < count; index++)
            {
                string steamId = panel.PlayerSteamIDs != null && index < panel.PlayerSteamIDs.Count
                    ? panel.PlayerSteamIDs[index]
                    : string.Empty;
                if (!verifiedSet.Contains(steamId) || currentRoster.ContainsKey(steamId))
                {
                    continue;
                }

                string richName = panel.IDInfos != null && index < panel.IDInfos.Count
                    ? ReadIdInfoName(panel.IDInfos[index])
                    : ReadTransformName(panel, index);
                CustomizationData customization = string.Equals(steamId, localSteamId, StringComparison.Ordinal)
                    ? GetLocalCustomizationData()
                    : GetCandidateCustomizationData(panel, index);
                currentRoster[steamId] = new RosterCloneState
                {
                    SteamId = steamId,
                    RichName = richName ?? string.Empty,
                    PlainName = SanitizeDisplayName(PlayerMentionProvider.StripRichText(richName)),
                    AvatarFingerprint = BuildCustomizationFingerprint(customization),
                };
            }

            ReconcileAbsentIncidents(verifiedSet);

            if (_previousRoster.Count > 0)
            {
                DetectNewCloneTransitions(currentRoster, localSteamId);
            }

            ReconcileNoLongerMatchingIncidents(currentRoster);

            _previousRoster.Clear();
            foreach (KeyValuePair<string, RosterCloneState> pair in currentRoster)
            {
                _previousRoster[pair.Key] = pair.Value;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning("Clone Shield scan skipped safely: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void DetectNewCloneTransitions(Dictionary<string, RosterCloneState> currentRoster, string localSteamId)
    {
        foreach (RosterCloneState culprit in currentRoster.Values)
        {
            bool culpritPreviouslyObserved = _previousRoster.TryGetValue(culprit.SteamId, out RosterCloneState previousCulprit);
            bool avatarChanged = !culpritPreviouslyObserved || !string.Equals(previousCulprit.AvatarFingerprint, culprit.AvatarFingerprint, StringComparison.Ordinal);
            bool nameChanged = !culpritPreviouslyObserved || !string.Equals(previousCulprit.RichName, culprit.RichName, StringComparison.Ordinal);
            if (!avatarChanged && !nameChanged)
            {
                continue;
            }

            foreach (RosterCloneState victim in currentRoster.Values)
            {
                if (string.Equals(victim.SteamId, culprit.SteamId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!_previousRoster.TryGetValue(victim.SteamId, out RosterCloneState previousVictim))
                {
                    continue;
                }

                CloneMatchKind match = CloneTransitionPolicy.Evaluate(
                    culpritPreviouslyObserved,
                    previousCulprit?.AvatarFingerprint,
                    culprit.AvatarFingerprint,
                    previousCulprit?.RichName,
                    culprit.RichName,
                    previousVictim.AvatarFingerprint,
                    victim.AvatarFingerprint,
                    previousVictim.RichName,
                    victim.RichName,
                    IsUsefulCustomizationFingerprint(culprit.AvatarFingerprint),
                    IsUsefulExactName(culprit.RichName));
                bool avatarMatches = (match & CloneMatchKind.AvatarOutfit) != 0;
                bool exactStyledNameMatches = (match & CloneMatchKind.ExactStyledName) != 0;
                if (!avatarMatches && !exactStyledNameMatches)
                {
                    continue;
                }

                string matchType = avatarMatches && exactStyledNameMatches
                    ? "avatar/outfit and exact styled name"
                    : avatarMatches ? "avatar/outfit" : "exact styled name";
                bool wasOpen = CloneIncidentLedger.Snapshot.Any(item =>
                    string.Equals(item.VictimSteamId, victim.SteamId, StringComparison.Ordinal) &&
                    string.Equals(item.SuspectSteamId, culprit.SteamId, StringComparison.Ordinal));
                CloneIncident incident = CloneIncidentLedger.Record(
                    victim.SteamId,
                    culprit.SteamId,
                    matchType,
                    victim.PlainName,
                    culprit.PlainName,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                if (incident != null)
                {
                    Patches.PlayerIdentityRoleHighlightController.RefreshAll();
                    if (!wasOpen) CloneIncidentActionController.RecordLifecycle("opened", incident, "verified", "clone-shield-local");
                }

                // Every QoL observer keeps local evidence. Only the potential
                // victim's own client publishes the cautious public warning, so
                // a crowded lobby does not echo the same alert from every client.
                if (incident != null && !string.IsNullOrWhiteSpace(localSteamId) &&
                    string.Equals(victim.SteamId, localSteamId, StringComparison.Ordinal))
                {
                    PublishPotentialClone(victim, culprit, matchType, incident.Id);
                }
            }
        }
    }

    private static string GetLocalSteamId()
    {
        try
        {
            return ((ulong)SteamUser.GetSteamID()).ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private void PublishPotentialClone(RosterCloneState victim, RosterCloneState culprit, string matchType, string incidentId)
    {
        string key = victim.SteamId + "|" + culprit.SteamId + "|" + matchType;
        if (_lastWarningTimes.TryGetValue(key, out float lastWarningAt) &&
            Time.unscaledTime - lastWarningAt < WarningCooldownSeconds)
        {
            return;
        }

        _lastWarningTimes[key] = Time.unscaledTime;
        string victimLabel = BuildPublicIdentityLabel(victim.PlainName, victim.SteamId);
        string culpritLabel = BuildPublicIdentityLabel(culprit.PlainName, culprit.SteamId);
        // Incident numbers are deliberately local to each observer. Keep them
        // out of the public warning so another client cannot mistake C001 for
        // a different local record; /sidlist is the authoritative local index.
        string message = "[Clone Shield] " + victimLabel + " was potentially cloned by " + culpritLabel +
                         " (" + matchType + "). Evidence IDs: victim ..." + PlayerIdentityEvidenceController.Suffix(victim.SteamId) +
                         "; suspect ..." + PlayerIdentityEvidenceController.Suffix(culprit.SteamId) +
                         "; match " + matchType +
                         ". Be aware of the player list, Steam ID, and profile; verify identity in case this alert is incorrect.";
        if (!Plugin.TrySendLobbyChatMessage(message))
        {
            Plugin.AddLocalNotificationWithToast(message + " Public send was unavailable.");
        }

        Plugin.Log?.LogInfo("Clone Shield public alert: victimSteam=…" + PlayerIdentityEvidenceController.Suffix(victim.SteamId) +
                            ", culpritSteam=…" + PlayerIdentityEvidenceController.Suffix(culprit.SteamId) + ", match=" + matchType + ".");
    }

    private static string BuildPublicIdentityLabel(string displayName, string steamId)
    {
        string safeName = string.IsNullOrWhiteSpace(displayName) ? "Unknown player" : displayName;
        string trimmed = (steamId ?? string.Empty).Trim();
        string suffix = trimmed.Length > 6 ? trimmed.Substring(trimmed.Length - 6) : trimmed;
        return safeName + " (ID ..." + (string.IsNullOrWhiteSpace(suffix) ? "unknown" : suffix) + ")";
    }

    private static bool IsUsefulExactName(string richName)
    {
        if (string.IsNullOrWhiteSpace(richName))
        {
            return false;
        }

        string plain = PlayerMentionProvider.StripRichText(richName).Trim();
        return plain.Length >= 2;
    }

    private static string SanitizeDisplayName(string displayName)
    {
        string safe = (displayName ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return safe.Length > 48 ? safe.Substring(0, 48) : safe;
    }

    private static bool TryGetActiveLobbyCode(out string lobbyCode)
    {
        lobbyCode = string.Empty;
        try
        {
            MultiplayerManager multiplayer = MonoSingleton<MultiplayerManager>.I;
            if (multiplayer == null || !multiplayer.LobbyStatus)
            {
                return false;
            }

            lobbyCode = multiplayer.LobbyCode?.Trim() ?? string.Empty;
            if (!ulong.TryParse(lobbyCode, out ulong rawLobby) || rawLobby == 0UL || !SteamManager.Initialized)
            {
                return false;
            }

            return true;
        }
        catch
        {
            lobbyCode = string.Empty;
            return false;
        }
    }

    private void ResetObservationState()
    {
        _observedLobbyCode = string.Empty;
        _previousRoster.Clear();
        _lastWarningTimes.Clear();
        _missingIncidentScans.Clear();
        _nonMatchingIncidentScans.Clear();
        _alignmentWaitWasLogged = false;
        _nextAlignmentWaitLogAt = 0f;
        CloneIncidentLedger.Reset(string.Empty);
        CloneIncidentActionController.ResetLobbyState();
    }

    private void LogAlignmentWait(string detail)
    {
        if (_alignmentWaitWasLogged && Time.unscaledTime < _nextAlignmentWaitLogAt) return;
        _alignmentWaitWasLogged = true;
        _nextAlignmentWaitLogAt = Time.unscaledTime + 60f;
        string reason = string.IsNullOrWhiteSpace(detail) ? "native/Steam roster verification is not ready" : detail;
        Plugin.Log?.LogDebug("Clone Shield waiting for aligned unique Steam/PurrNet/Steam-lobby roster before recording evidence: " + reason + ". Further identical waits are suppressed for 60 seconds.");
    }

    private void ReconcileAbsentIncidents(HashSet<string> verifiedSteamIds)
    {
        foreach (CloneIncident incident in CloneIncidentLedger.Snapshot.ToArray())
        {
            bool bothPresent = verifiedSteamIds.Contains(incident.VictimSteamId) && verifiedSteamIds.Contains(incident.SuspectSteamId);
            if (bothPresent)
            {
                _missingIncidentScans.Remove(incident.StableId);
                continue;
            }

            int misses = _missingIncidentScans.TryGetValue(incident.StableId, out int prior) ? prior + 1 : 1;
            _missingIncidentScans[incident.StableId] = misses;
            if (misses < 2) continue;
            if (CloneIncidentActionController.MarkStaleAbsent(incident.Id, out _))
            {
                _missingIncidentScans.Remove(incident.StableId);
                Patches.PlayerIdentityRoleHighlightController.RefreshAll();
            }
        }
    }

    private void ReconcileNoLongerMatchingIncidents(Dictionary<string, RosterCloneState> currentRoster)
    {
        foreach (CloneIncident incident in CloneIncidentLedger.Snapshot.ToArray())
        {
            if (!currentRoster.TryGetValue(incident.VictimSteamId, out RosterCloneState victim) ||
                !currentRoster.TryGetValue(incident.SuspectSteamId, out RosterCloneState suspect))
            {
                _nonMatchingIncidentScans.Remove(incident.StableId);
                continue;
            }

            bool tracksAvatar = incident.MatchType.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0;
            bool tracksName = incident.MatchType.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0;
            bool avatarStillMatches = tracksAvatar &&
                IsUsefulCustomizationFingerprint(victim.AvatarFingerprint) &&
                string.Equals(victim.AvatarFingerprint, suspect.AvatarFingerprint, StringComparison.Ordinal);
            bool nameStillMatches = tracksName && IsUsefulExactName(victim.RichName) &&
                string.Equals(victim.RichName, suspect.RichName, StringComparison.Ordinal);
            if (avatarStillMatches || nameStillMatches)
            {
                _nonMatchingIncidentScans.Remove(incident.StableId);
                continue;
            }

            int misses = _nonMatchingIncidentScans.TryGetValue(incident.StableId, out int prior) ? prior + 1 : 1;
            _nonMatchingIncidentScans[incident.StableId] = misses;
            if (misses < 2) continue;
            if (CloneIncidentActionController.MarkNoLongerMatching(incident.Id, out _))
            {
                _nonMatchingIncidentScans.Remove(incident.StableId);
                Patches.PlayerIdentityRoleHighlightController.RefreshAll();
            }
        }
    }

    private static string ReadIdInfoName(object idInfo)
    {
        if (idInfo == null)
        {
            return string.Empty;
        }

        object nameValue = AccessTools.Field(idInfo.GetType(), "Name")?.GetValue(idInfo);
        if (nameValue is string text)
        {
            return text;
        }

        if (nameValue is byte[] bytes && bytes.Length > 0)
        {
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }

        return string.Empty;
    }

    private static string ReadTransformName(PlayerPanelController panel, int index)
    {
        try
        {
            if (panel?.PlayerTransforms == null || index < 0 || index >= panel.PlayerTransforms.Count)
            {
                return string.Empty;
            }

            NetworkTransform networkTransform = panel.PlayerTransforms[index];
            PlayerController player = networkTransform != null ? networkTransform.GetComponent<PlayerController>() : null;
            if (player?.PlayerNameText != null)
            {
                return player.PlayerNameText.text ?? string.Empty;
            }

            return networkTransform != null ? networkTransform.name : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static CustomizationData GetLocalCustomizationData()
    {
        try
        {
            return MonoSingleton<DataManager>.I?.CustomizationData;
        }
        catch
        {
            return null;
        }
    }

    private static CustomizationData GetCandidateCustomizationData(PlayerPanelController panel, int index)
    {
        try
        {
            if (panel?.PlayerTransforms == null || index < 0 || index >= panel.PlayerTransforms.Count)
            {
                return null;
            }

            NetworkTransform networkTransform = panel.PlayerTransforms[index];
            PlayerCustomizationController customization = networkTransform != null
                ? networkTransform.GetComponent<PlayerCustomizationController>()
                : null;
            return customization != null ? CustomizationDataField?.GetValue(customization) as CustomizationData : null;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCustomizationFingerprint(CustomizationData customizationData)
    {
        if (customizationData == null)
        {
            return string.Empty;
        }

        try
        {
            var ids = new CustomizationDataIDs3(customizationData);
            var builder = new StringBuilder();
            builder.Append("full=").Append(ids.IsFullBody ? "1" : "0");
            AppendShapeMap(builder, "A", ids.AppearanceToShapeAndColors);
            AppendShapeMap(builder, "O", ids.OutfitToShapeAndColors);
            return builder.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void AppendShapeMap<TKey>(StringBuilder builder, string prefix, IDictionary<TKey, ShapeAndColorsData> shapes)
    {
        if (builder == null || shapes == null)
        {
            return;
        }

        foreach (KeyValuePair<TKey, ShapeAndColorsData> pair in shapes.OrderBy(pair => Convert.ToInt32(pair.Key)))
        {
            builder.Append('|').Append(prefix).Append(':').Append(Convert.ToInt32(pair.Key)).Append('=');
            builder.Append(pair.Value.ShapeIndex).Append(':');
            if (pair.Value.ColorIndexes == null || pair.Value.ColorIndexes.Count == 0)
            {
                continue;
            }

            for (int index = 0; index < pair.Value.ColorIndexes.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(pair.Value.ColorIndexes[index]);
            }
        }
    }

    private static bool IsUsefulCustomizationFingerprint(string fingerprint)
    {
        return !string.IsNullOrWhiteSpace(fingerprint) &&
               fingerprint.Contains("|A:") &&
               fingerprint.Contains("|O:") &&
               fingerprint.Length > 32;
    }
}
