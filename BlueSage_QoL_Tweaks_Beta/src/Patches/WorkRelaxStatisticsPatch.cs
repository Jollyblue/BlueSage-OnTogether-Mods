#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BlueSage.QoLTweaks.Core;
using HarmonyLib;

namespace BlueSage.QoLTweaks.Patches;

[HarmonyPatch]
internal static class WorkRelaxStatisticsPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(
                candidate.GetName().Name,
                "Assembly-CSharp",
                StringComparison.Ordinal));
        if (assembly == null)
        {
            WorkRelaxRuntimeController.LogOnce("targets-assembly", "Work / Relax native observation is unavailable on this game build.");
            yield break;
        }

        Type? statisticManager = assembly.GetType("StatisticManager", false);
        Type? focusType = assembly.GetType("FocusType", false);
        foreach (MethodBase method in ResolveStatisticTargets(statisticManager, focusType))
        {
            yield return method;
        }

        Type? todoController = assembly.GetType("TodoController", false);
        Type? todoItemController = assembly.GetType("ToDoItemController", false);
        MethodInfo? toggleTodo = FindExact(todoController, "ToggleTodo", todoItemController == null ? null : new[] { todoItemController });
        if (toggleTodo != null)
        {
            yield return toggleTodo;
        }
        else
        {
            WorkRelaxRuntimeController.LogOnce("target-toggle-todo", "Work / Relax task-check observation is unavailable on this game build.");
        }

        Type? dataManager = assembly.GetType("DataManager", false);
        MethodInfo? savePlayerStatistics = FindExact(dataManager, "SavePlayerStatistics", Type.EmptyTypes);
        if (savePlayerStatistics != null)
        {
            yield return savePlayerStatistics;
        }
        else
        {
            WorkRelaxRuntimeController.LogOnce("target-save-statistics", "Work / Relax reconciliation observation is unavailable on this game build.");
        }
    }

    [HarmonyPrefix]
    private static void Prefix(MethodBase __originalMethod, object[] __args, out WorkRelaxObservationState? __state)
    {
        if (IsActivityStart(__originalMethod) && __args.Length > 0 && __args[0] != null)
        {
            WorkRelaxRuntimeController.BeginActivityInterval(__args[0].ToString() ?? string.Empty);
        }

        __state = IsNativeSave(__originalMethod)
            ? null
            : WorkRelaxRuntimeController.BeginObservation(
                SourceName(__originalMethod),
                IsTodo(__originalMethod) ? WorkRelaxObservationKind.Todo : WorkRelaxObservationKind.NativeStatistics);
    }

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod, WorkRelaxObservationState? __state)
    {
        if (IsNativeSave(__originalMethod))
        {
            WorkRelaxRuntimeController.ReconcileAfterNativeSave();
            return;
        }

        WorkRelaxRuntimeController.CompleteObservation(__state);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception, MethodBase __originalMethod, WorkRelaxObservationState? __state)
    {
        if (__exception != null && !IsNativeSave(__originalMethod))
        {
            WorkRelaxRuntimeController.AbortObservation(__state);
        }

        return __exception;
    }

    private static IEnumerable<MethodBase> ResolveStatisticTargets(Type? owner, Type? focusType)
    {
        MethodInfo? pomodoro = FindExact(owner, "UpdatePomodoroTime", Type.EmptyTypes);
        MethodInfo? sessions = FindExact(owner, "UpdateFocusStatistics", Type.EmptyTypes);
        MethodInfo? activityCount = FindExact(owner, "UpdateFocusTypeCountStatistics", focusType == null ? null : new[] { focusType });
        MethodInfo? activityDuration = FindExact(owner, "UpdateStatisticsDurations", focusType == null ? null : new[] { focusType });

        if (pomodoro != null) yield return pomodoro;
        else LogMissingStatisticTarget("pomodoro-time");

        if (sessions != null) yield return sessions;
        else LogMissingStatisticTarget("focus-session");

        if (activityCount != null) yield return activityCount;
        else LogMissingStatisticTarget("activity-start");

        if (activityDuration != null) yield return activityDuration;
        else LogMissingStatisticTarget("activity-duration");
    }

    private static void LogMissingStatisticTarget(string key) =>
        WorkRelaxRuntimeController.LogOnce("target-" + key,
            "One optional Work / Relax native observation is unavailable on this game build: " + key + ".");

    private static MethodInfo? FindExact(Type? owner, string name, Type[]? parameters)
    {
        if (owner == null || parameters == null)
        {
            return null;
        }

        return owner.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            parameters,
            null);
    }

    private static bool IsNativeSave(MethodBase method) =>
        string.Equals(method.DeclaringType?.Name, "DataManager", StringComparison.Ordinal) &&
        string.Equals(method.Name, "SavePlayerStatistics", StringComparison.Ordinal);

    private static bool IsTodo(MethodBase method) =>
        string.Equals(method.DeclaringType?.Name, "TodoController", StringComparison.Ordinal) &&
        string.Equals(method.Name, "ToggleTodo", StringComparison.Ordinal);

    private static bool IsActivityStart(MethodBase method) =>
        string.Equals(method.DeclaringType?.Name, "StatisticManager", StringComparison.Ordinal) &&
        string.Equals(method.Name, "UpdateFocusTypeCountStatistics", StringComparison.Ordinal);

    private static string SourceName(MethodBase method) =>
        (method.DeclaringType?.Name ?? "Native") + "." + method.Name;
}

[HarmonyPatch]
internal static class WorkRelaxDurationLifecyclePatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => string.Equals(
                candidate.GetName().Name,
                "Assembly-CSharp",
                StringComparison.Ordinal));
        if (assembly == null)
        {
            WorkRelaxRuntimeController.LogOnce("lifecycle-targets-assembly",
                "Work / Relax duration lifecycle observation is unavailable on this game build.");
            yield break;
        }

        Type? pomodoroController = assembly.GetType("PomodoroController", false);
        Type? pomodoroType = assembly.GetType("PomodoroType", false);
        MethodInfo? pomodoroTypeSetter = FindExact(
            pomodoroController,
            "set_PomodoroType",
            pomodoroType == null ? null : new[] { pomodoroType });
        MethodInfo? pausedSetter = FindExact(pomodoroController, "set_IsPaused", new[] { typeof(bool) });
        Type? playerFocusController = assembly.GetType("PlayerFocusController", false);
        MethodInfo? focusSetter = FindExact(playerFocusController, "set_IsFocus", new[] { typeof(bool) });

        if (pomodoroTypeSetter != null) yield return pomodoroTypeSetter;
        else LogMissingLifecycleTarget("pomodoro-type");

        if (pausedSetter != null) yield return pausedSetter;
        else LogMissingLifecycleTarget("pomodoro-pause");

        if (focusSetter != null) yield return focusSetter;
        else LogMissingLifecycleTarget("activity-end");
    }

    [HarmonyPostfix]
    private static void Postfix(MethodBase __originalMethod, object __instance, object[] __args)
    {
        try
        {
            if (string.Equals(__originalMethod.DeclaringType?.Name, "PomodoroController", StringComparison.Ordinal))
            {
                string pomodoroType = ReadProperty(__instance, "PomodoroType")?.ToString() ?? string.Empty;
                bool isPaused = ReadProperty(__instance, "IsPaused") is bool paused && paused;
                WorkRelaxRuntimeController.ObservePomodoroState(
                    string.Equals(pomodoroType, "Study", StringComparison.Ordinal),
                    isPaused);
                return;
            }

            if (string.Equals(__originalMethod.DeclaringType?.Name, "PlayerFocusController", StringComparison.Ordinal) &&
                __args.Length > 0 && __args[0] is bool isFocus && !isFocus &&
                __instance is UnityEngine.Component component && BetterMoveMovementPatch.IsLocalOwner(component))
            {
                WorkRelaxRuntimeController.DiscardActivityIntervals();
            }
        }
        catch (Exception exception)
        {
            WorkRelaxRuntimeController.LogOnce("duration-lifecycle-" + exception.GetType().Name,
                "One Work / Relax duration lifecycle observation failed open: " +
                exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static object? ReadProperty(object instance, string name) =>
        instance.GetType().GetProperty(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance, null);

    private static MethodInfo? FindExact(Type? owner, string name, Type[]? parameters)
    {
        if (owner == null || parameters == null)
        {
            return null;
        }

        return owner.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            parameters,
            null);
    }

    private static void LogMissingLifecycleTarget(string key) =>
        WorkRelaxRuntimeController.LogOnce("lifecycle-target-" + key,
            "One optional Work / Relax duration lifecycle boundary is unavailable on this game build: " + key + ".");
}
