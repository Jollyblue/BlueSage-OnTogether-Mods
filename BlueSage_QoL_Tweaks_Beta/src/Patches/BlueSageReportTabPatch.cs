#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch]
internal static class BlueSageReportTabPatch
{
    private static readonly BlueSageReportAnchorGate Gate = new();
    private static readonly BlueSageReportControllerRegistry Controllers = new();

    [HarmonyPrepare]
    private static bool Prepare()
    {
        Type? manager = FindManagerType();
        bool ready = manager != null && AccessTools.Method(manager, "OpenStatisticsPanel", Type.EmptyTypes) != null;
        if (!ready) WorkRelaxRuntimeController.LogOnce("report-tab-lifecycle",
            BlueSageReportSurfacePolicy.LifecycleFailOpenDiagnostic);
        return ready;
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type? manager = FindManagerType();
        if (manager == null) yield break;
        MethodInfo? open = AccessTools.Method(manager, "OpenStatisticsPanel", Type.EmptyTypes);
        MethodInfo? close = AccessTools.Method(manager, "CloseStatisticsPanel", Type.EmptyTypes);
        if (open != null) yield return open;
        if (close != null) yield return close;
    }

    [HarmonyPrefix]
    private static void BeforeNativeReportLifecycle(MethodBase __originalMethod)
    {
        if (string.Equals(__originalMethod.Name, "CloseStatisticsPanel", StringComparison.Ordinal))
            RestoreNativeDefault();
    }

    [HarmonyPostfix]
    private static void AfterNativeReportLifecycle(MethodBase __originalMethod, object __instance)
    {
        if (!string.Equals(__originalMethod.Name, "OpenStatisticsPanel", StringComparison.Ordinal)) return;
        try
        {
            BlueSageReportTabController? controller = TryAttach(__instance as Component);
            controller?.Reopen();
        }
        catch (Exception exception)
        {
            WorkRelaxRuntimeController.LogOnce("report-tab-runtime",
                "BlueSage Report tab disabled safely: " + exception.GetType().Name + ": " + exception.Message);
        }
    }

    internal static void RestoreNativeDefault()
    {
        if (Controllers.RestoreAll() == 0)
        {
            Plugin.CloseBlueSageReportWindowOnly();
        }
    }

    private static BlueSageReportTabController? TryAttach(Component? statisticManager)
    {
        BlueSageJournalMoodTextureResolver.ObserveStatisticManager(statisticManager);
        FieldInfo? panelField = statisticManager?.GetType().GetField("_statisticsPanel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject? nativePanel = panelField?.GetValue(statisticManager) as GameObject;
        Transform? reportRoot = nativePanel?.transform;
        Transform? nativeTab = statisticManager == null ? null :
            ExactDescendant(statisticManager.transform.root, "ButtonStatistics");
        Button? nativeButton = nativeTab?.GetComponent<Button>();
        TMP_Text? nativeTmp = nativeTab?.GetComponentInChildren<TMP_Text>(true);
        Text? nativeText = nativeTab?.GetComponentInChildren<Text>(true);
        Type? managerType = FindManagerType();
        bool lifecycle = managerType != null &&
            AccessTools.Method(managerType, "OpenStatisticsPanel", Type.EmptyTypes) != null &&
            AccessTools.Method(managerType, "CloseStatisticsPanel", Type.EmptyTypes) != null;
        BlueSageReportAnchors anchors = new(statisticManager != null, reportRoot != null, nativeTab != null,
            lifecycle, nativeButton != null, nativeTmp != null || nativeText != null);
        if (!Gate.TryEnable(anchors, () => WorkRelaxRuntimeController.LogOnce("report-tab-anchors",
                BlueSageReportSurfacePolicy.FailOpenDiagnostic))) return null;

        int rootId = reportRoot!.GetInstanceID();
        Controllers.Prune();
        if (Controllers.TryGet(rootId, out BlueSageReportTabController? existing)) return existing;

        GameObject panel = new("BlueSage_ReportPanel", typeof(RectTransform));
        panel.transform.SetParent(reportRoot.parent, false);
        CopyRect(reportRoot as RectTransform, panel.GetComponent<RectTransform>());
        panel.SetActive(false);

        GameObject tab = UnityEngine.Object.Instantiate(nativeTab!.gameObject, nativeTab.parent, false);
        tab.name = "BlueSage_ReportTab";
        SetLabel(tab, "BLUESAGE REPORT");
        Button button = tab.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();

        var controller = new BlueSageReportTabController(
            value => nativePanel!.SetActive(value), value => panel.SetActive(value),
            Plugin.OpenBlueSageReport, Plugin.CloseBlueSageReportWindowOnly,
            () => nativePanel != null && panel != null && tab != null);
        if (!controller.TryClaimAttachment(rootId))
        {
            UnityEngine.Object.Destroy(tab);
            UnityEngine.Object.Destroy(panel);
            return null;
        }
        button.onClick.AddListener(controller.SelectBlueSage);
        nativeButton!.onClick.AddListener(controller.SelectNative);
        Controllers.Register(rootId, controller);
        return controller;
    }

    private static Type? FindManagerType() => AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))?
        .GetType("StatisticManager", false);

    private static Transform? ExactDescendant(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(child.name, name, StringComparison.Ordinal)) return child;
        return null;
    }

    private static void CopyRect(RectTransform? source, RectTransform target)
    {
        if (source == null) return;
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
    }

    private static void SetLabel(GameObject target, string label)
    {
        TMP_Text? tmp = target.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) { tmp.text = label; return; }
        target.GetComponentInChildren<Text>(true)!.text = label;
    }
}
