using System;
using System.Collections.Generic;
using System.Linq;
using BlueSage.QoLTweaks.Core;
using PurrNet;
using Steamworks;
using UnityEngine;

namespace BlueSage.QoLTweaks;

internal sealed class PlayerRenderSaverController : MonoBehaviour
{
    private static readonly Dictionary<Renderer, SavedRendererState> RendererStates = new Dictionary<Renderer, SavedRendererState>();
    private static readonly Dictionary<Animator, SavedAnimatorState> AnimatorStates = new Dictionary<Animator, SavedAnimatorState>();
    private static readonly Dictionary<ParticleSystem, SavedParticleState> ParticleStates = new Dictionary<ParticleSystem, SavedParticleState>();
    private static readonly Dictionary<AudioSource, SavedAudioState> AudioStates = new Dictionary<AudioSource, SavedAudioState>();
    private static readonly Dictionary<int, CachedVisualSet> VisualCache = new Dictionary<int, CachedVisualSet>();
    private static readonly HashSet<int> HiddenOwners = new HashSet<int>();
    private float _nextRefreshAt;

    internal static int LastHiddenRemotePlayers { get; private set; }
    internal static int LastVisibleRemotePlayers { get; private set; }
    internal static int LastCandidateRemotePlayers { get; private set; }
    internal static int LastSuppressedRenderers { get; private set; }
    internal static string LastRuntimeState { get; private set; } = "not scanned yet";

    private void Update()
    {
        if (Time.unscaledTime < _nextRefreshAt)
        {
            return;
        }

        _nextRefreshAt = Time.unscaledTime + PlayerRenderSaverPolicy.RefreshIntervalSeconds;
        try
        {
            Refresh();
        }
        catch (Exception ex)
        {
            LastRuntimeState = "scan failed: " + ex.GetType().Name;
            Plugin.Log?.LogWarning("Player Render Saver scan failed safely: " + ex.GetType().Name + ": " + ex.Message);
            RestoreAll("scan failed; renderers restored");
        }
    }

    private void OnDestroy()
    {
        RestoreAll();
    }

    private static void Refresh()
    {
        if (!Plugin.ShouldApplyPlayerRenderSaver)
        {
            RestoreAll("off");
            return;
        }

        if (!TryGetLocalPosition(out Vector3 localPosition))
        {
            RestoreAll("waiting for local player");
            return;
        }

        List<RemotePlayerRenderCandidate> candidates = CollectRemoteCandidates(localPosition)
            .OrderBy(candidate => candidate.DistanceMeters)
            .ToList();
        LastCandidateRemotePlayers = candidates.Count;
        var hiddenOwnerIds = new HashSet<int>();
        int hidden = 0;
        int visible = 0;

        for (int rank = 0; rank < candidates.Count; rank++)
        {
            RemotePlayerRenderCandidate candidate = candidates[rank];
            bool hide = PlayerRenderSaverPolicy.ShouldHideRemotePlayer(
                candidate.DistanceMeters,
                rank,
                Plugin.LockedPlayerRenderSaverRadiusMeters,
                Plugin.LockedPlayerRenderSaverMaxVisiblePlayers,
                HiddenOwners.Contains(candidate.Root.GetInstanceID()));
            int ownerId = candidate.Root.GetInstanceID();
            if (hide)
            {
                hiddenOwnerIds.Add(ownerId);
                HideRoot(candidate.Root, ownerId);
                hidden++;
            }
            else
            {
                RestoreRoot(ownerId);
                visible++;
            }
        }

        RestoreStale(hiddenOwnerIds);
        LastHiddenRemotePlayers = hidden;
        LastVisibleRemotePlayers = visible;
        LastSuppressedRenderers = RendererStates.Count;
        LastRuntimeState = candidates.Count == 0
            ? "no remote player roots found"
            : hidden == 0
                ? "active; every remote player is inside the current budgets"
                : LastSuppressedRenderers == 0
                    ? "players selected for hiding, but no avatar renderers were found under their panel roots"
                    : "active; avatar renderers suppressed";
    }

    internal static void RestoreAll()
    {
        RestoreAll(Plugin.ShouldApplyPlayerRenderSaver ? "restored" : "off");
    }

    private static void RestoreAll(string runtimeState)
    {
        foreach (KeyValuePair<Renderer, SavedRendererState> state in RendererStates.ToArray())
        {
            if (state.Key != null)
            {
                state.Key.enabled = state.Value.Enabled;
            }
        }

        foreach (KeyValuePair<Animator, SavedAnimatorState> state in AnimatorStates.ToArray())
        {
            if (state.Key != null)
            {
                state.Key.cullingMode = state.Value.CullingMode;
            }
        }

        foreach (KeyValuePair<ParticleSystem, SavedParticleState> state in ParticleStates.ToArray())
        {
            if (state.Key != null)
            {
                ParticleSystem.EmissionModule emission = state.Key.emission;
                emission.enabled = state.Value.EmissionEnabled;
            }
        }

        foreach (KeyValuePair<AudioSource, SavedAudioState> state in AudioStates.ToArray())
        {
            if (state.Key != null) state.Key.mute = state.Value.Muted;
        }

        RendererStates.Clear();
        AnimatorStates.Clear();
        ParticleStates.Clear();
        AudioStates.Clear();
        VisualCache.Clear();
        HiddenOwners.Clear();
        LastHiddenRemotePlayers = 0;
        LastVisibleRemotePlayers = 0;
        LastCandidateRemotePlayers = 0;
        LastSuppressedRenderers = 0;
        LastRuntimeState = runtimeState;
    }

    private static IEnumerable<RemotePlayerRenderCandidate> CollectRemoteCandidates(Vector3 localPosition)
    {
        PlayerPanelController panel = NetworkSingleton<PlayerPanelController>.I;
        if (panel?.PlayerTransforms == null || panel.PlayerTransforms.Count == 0)
        {
            yield break;
        }

        string localSteamId = GetLocalSteamId();
        int count = panel.PlayerTransforms.Count;
        for (int index = 0; index < count; index++)
        {
            NetworkTransform networkTransform = panel.PlayerTransforms[index];
            Transform root = networkTransform != null ? networkTransform.transform : null;
            if (root == null)
            {
                continue;
            }

            string steamId = panel.PlayerSteamIDs != null && index < panel.PlayerSteamIDs.Count
                ? panel.PlayerSteamIDs[index]
                : string.Empty;
            if (!string.IsNullOrEmpty(localSteamId) && string.Equals(steamId, localSteamId, StringComparison.Ordinal))
            {
                continue;
            }

            yield return new RemotePlayerRenderCandidate(root, Vector3.Distance(localPosition, root.position));
        }
    }

    private static bool TryGetLocalPosition(out Vector3 position)
    {
        position = Vector3.zero;
        try
        {
            TextChannelManager textChannelManager = NetworkSingleton<TextChannelManager>.I;
            if (textChannelManager?.MainPlayer == null)
            {
                return false;
            }

            position = textChannelManager.MainPlayer.position;
            return true;
        }
        catch
        {
            return false;
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

    private static void HideRoot(Transform root, int ownerId)
    {
        CachedVisualSet visuals = GetOrCreateVisualSet(root, ownerId);
        foreach (Renderer renderer in visuals.Renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!RendererStates.ContainsKey(renderer))
            {
                RendererStates[renderer] = new SavedRendererState(renderer.enabled, ownerId);
            }

            renderer.enabled = false;
        }

        // Keep Canvas/TMP nameplates and interaction UI alive. Only avatar visuals and
        // presentation-only work are degraded; networking and ownership components remain untouched.
        foreach (Animator animator in visuals.Animators)
        {
            if (animator == null) continue;
            if (!AnimatorStates.ContainsKey(animator))
            {
                AnimatorStates[animator] = new SavedAnimatorState(animator.cullingMode, ownerId);
            }
            animator.cullingMode = AnimatorCullingMode.CullCompletely;
        }

        foreach (ParticleSystem particle in visuals.Particles)
        {
            if (particle == null) continue;
            if (!ParticleStates.ContainsKey(particle))
            {
                ParticleStates[particle] = new SavedParticleState(particle.emission.enabled, ownerId);
            }
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = false;
        }

        foreach (AudioSource audio in visuals.AudioSources)
        {
            if (audio == null || IsEssentialAudio(audio)) continue;
            if (!AudioStates.ContainsKey(audio)) AudioStates[audio] = new SavedAudioState(audio.mute, ownerId);
            audio.mute = true;
        }

        HiddenOwners.Add(ownerId);
    }

    private static bool IsEssentialAudio(AudioSource audio)
    {
        // Never silence global/UI audio or voice-chat surfaces. Only spatial avatar foley is
        // eligible for local LOD muting; Vivox may attach sources beneath remote player roots.
        if (audio.spatialBlend <= 0.01f) return true;
        Transform cursor = audio.transform;
        for (int depth = 0; cursor != null && depth < 6; depth++, cursor = cursor.parent)
        {
            string name = cursor.name ?? string.Empty;
            if (name.IndexOf("voice", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("vivox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("speech", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    private static void RestoreRoot(int ownerId)
    {
        foreach (KeyValuePair<Renderer, SavedRendererState> state in RendererStates.ToArray())
        {
            if (state.Value.OwnerId != ownerId)
            {
                continue;
            }

            if (state.Key != null)
            {
                state.Key.enabled = state.Value.Enabled;
            }

            RendererStates.Remove(state.Key);
        }

        foreach (KeyValuePair<Animator, SavedAnimatorState> state in AnimatorStates.ToArray())
        {
            if (state.Value.OwnerId != ownerId)
            {
                continue;
            }

            if (state.Key != null)
            {
                state.Key.cullingMode = state.Value.CullingMode;
            }
            AnimatorStates.Remove(state.Key);
        }
        RestoreOwnedParticles(ownerId);
        RestoreOwnedAudio(ownerId);
        HiddenOwners.Remove(ownerId);
    }

    private static void RestoreStale(HashSet<int> hiddenOwnerIds)
    {
        foreach (KeyValuePair<Renderer, SavedRendererState> state in RendererStates.ToArray())
        {
            if (state.Key == null || !hiddenOwnerIds.Contains(state.Value.OwnerId))
            {
                if (state.Key != null)
                {
                    state.Key.enabled = state.Value.Enabled;
                }

                RendererStates.Remove(state.Key);
            }
        }

        foreach (int ownerId in HiddenOwners.ToArray())
        {
            if (!hiddenOwnerIds.Contains(ownerId)) RestoreRoot(ownerId);
        }

        foreach (int cachedOwnerId in VisualCache.Keys.ToArray())
        {
            CachedVisualSet cached = VisualCache[cachedOwnerId];
            if (cached.Root == null) VisualCache.Remove(cachedOwnerId);
        }
    }

    private static CachedVisualSet GetOrCreateVisualSet(Transform root, int ownerId)
    {
        if (VisualCache.TryGetValue(ownerId, out CachedVisualSet cached) && cached.Root == root) return cached;
        cached = new CachedVisualSet(root,
            root.GetComponentsInChildren<Renderer>(true),
            root.GetComponentsInChildren<Animator>(true),
            root.GetComponentsInChildren<ParticleSystem>(true),
            root.GetComponentsInChildren<AudioSource>(true));
        VisualCache[ownerId] = cached;
        return cached;
    }

    private static void RestoreOwnedParticles(int ownerId)
    {
        foreach (KeyValuePair<ParticleSystem, SavedParticleState> state in ParticleStates.ToArray())
        {
            if (state.Value.OwnerId != ownerId) continue;
            if (state.Key != null) { ParticleSystem.EmissionModule emission = state.Key.emission; emission.enabled = state.Value.EmissionEnabled; }
            ParticleStates.Remove(state.Key);
        }
    }

    private static void RestoreOwnedAudio(int ownerId)
    {
        foreach (KeyValuePair<AudioSource, SavedAudioState> state in AudioStates.ToArray())
        {
            if (state.Value.OwnerId != ownerId) continue;
            if (state.Key != null) state.Key.mute = state.Value.Muted;
            AudioStates.Remove(state.Key);
        }
    }

    private readonly struct RemotePlayerRenderCandidate
    {
        public RemotePlayerRenderCandidate(Transform root, float distanceMeters)
        {
            Root = root;
            DistanceMeters = distanceMeters;
        }

        public Transform Root { get; }
        public float DistanceMeters { get; }
    }

    private readonly struct SavedRendererState
    {
        public SavedRendererState(bool enabled, int ownerId)
        {
            Enabled = enabled;
            OwnerId = ownerId;
        }

        public bool Enabled { get; }
        public int OwnerId { get; }
    }

    private readonly struct SavedAnimatorState
    {
        public SavedAnimatorState(AnimatorCullingMode cullingMode, int ownerId) { CullingMode = cullingMode; OwnerId = ownerId; }
        public AnimatorCullingMode CullingMode { get; }
        public int OwnerId { get; }
    }

    private readonly struct SavedParticleState
    {
        public SavedParticleState(bool emissionEnabled, int ownerId) { EmissionEnabled = emissionEnabled; OwnerId = ownerId; }
        public bool EmissionEnabled { get; }
        public int OwnerId { get; }
    }

    private readonly struct SavedAudioState
    {
        public SavedAudioState(bool muted, int ownerId) { Muted = muted; OwnerId = ownerId; }
        public bool Muted { get; }
        public int OwnerId { get; }
    }

    private sealed class CachedVisualSet
    {
        public CachedVisualSet(Transform root, Renderer[] renderers, Animator[] animators, ParticleSystem[] particles, AudioSource[] audioSources)
        { Root = root; Renderers = renderers; Animators = animators; Particles = particles; AudioSources = audioSources; }
        public Transform Root { get; }
        public Renderer[] Renderers { get; }
        public Animator[] Animators { get; }
        public ParticleSystem[] Particles { get; }
        public AudioSource[] AudioSources { get; }
    }
}
