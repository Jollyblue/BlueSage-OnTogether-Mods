using System;
using System.Collections.Generic;
using BlueSage.QoLTweaks.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace BlueSage.QoLTweaks;

internal static class SweepResourceEvidenceCapture
{
    internal const int MaximumAssetFacts = 50000;

    public static SweepResourceSnapshot Capture()
    {
        try
        {
            List<SweepResourceCategoryTotal> categories = new List<SweepResourceCategoryTotal>(5);
            List<SweepResourceAssetFact> assets = new List<SweepResourceAssetFact>(Math.Min(MaximumAssetFacts, 8192));
            bool detailTruncated = false;

            CaptureResourceType(Resources.FindObjectsOfTypeAll<Texture2D>(), "Texture2D", categories, assets, ref detailTruncated);
            CaptureResourceType(Resources.FindObjectsOfTypeAll<RenderTexture>(), "RenderTexture", categories, assets, ref detailTruncated);
            CaptureResourceType(Resources.FindObjectsOfTypeAll<Material>(), "Material", categories, assets, ref detailTruncated);
            CaptureResourceType(Resources.FindObjectsOfTypeAll<Mesh>(), "Mesh", categories, assets, ref detailTruncated);
            CaptureResourceType(Resources.FindObjectsOfTypeAll<AudioClip>(), "AudioClip", categories, assets, ref detailTruncated);

            return new SweepResourceSnapshot
            {
                Captured = true,
                Status = "ok",
                SceneObjectCount = Resources.FindObjectsOfTypeAll<GameObject>().Length,
                Categories = categories.ToArray(),
                Assets = assets.ToArray(),
                DetailTruncated = detailTruncated
            };
        }
        catch (Exception ex)
        {
            return SweepResourceSnapshot.Unavailable(ex.GetType().Name);
        }
    }

    private static void CaptureResourceType<T>(
        T[] resources,
        string category,
        List<SweepResourceCategoryTotal> categories,
        List<SweepResourceAssetFact> assets,
        ref bool detailTruncated) where T : UnityEngine.Object
    {
        int count = 0;
        long runtimeBytes = 0L;
        if (resources != null)
        {
            foreach (T resource in resources)
            {
                if (resource == null) continue;

                count++;
                long bytes = Profiler.GetRuntimeMemorySizeLong(resource);
                if (bytes < 0L) bytes = 0L;
                runtimeBytes += bytes;

                if (assets.Count < MaximumAssetFacts)
                {
                    assets.Add(new SweepResourceAssetFact(
                        resource.GetInstanceID(),
                        category,
                        bytes));
                }
                else
                {
                    detailTruncated = true;
                }
            }
        }

        categories.Add(new SweepResourceCategoryTotal(category, count, runtimeBytes));
    }
}
