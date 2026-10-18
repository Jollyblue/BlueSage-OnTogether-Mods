using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal static class WorkRelaxCommandPolicy
{
    internal static void RegisterCommands(
        BlueSageCommandRegistry registry,
        Func<WorkRelaxMode?> readMode,
        Func<WorkRelaxMode, bool> setMode)
    {
        if (registry == null) throw new ArgumentNullException(nameof(registry));
        if (readMode == null) throw new ArgumentNullException(nameof(readMode));
        if (setMode == null) throw new ArgumentNullException(nameof(setMode));

        Register(registry, "workon", "Select Work mode", readMode, setMode);
        Register(registry, "relaxon", "Select Relax mode", readMode, setMode);
        Register(registry, "workoff", "Switch from Work to Relax", readMode, setMode);
        Register(registry, "relaxoff", "Switch from Relax to Work", readMode, setMode);
        Register(registry, "mode", "Select Work or Relax mode", readMode, setMode);
    }

    public static bool TryResolve(
        string commandName,
        IReadOnlyList<string> tokens,
        out WorkRelaxMode mode,
        out string error)
    {
        mode = default;
        error = string.Empty;

        string command = (commandName ?? string.Empty).Trim();
        if (command.StartsWith("/", StringComparison.Ordinal))
        {
            command = command.Substring(1);
        }

        if (string.Equals(command, "workon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "relaxoff", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDirect(WorkRelaxMode.Work, tokens, out mode, out error);
        }

        if (string.Equals(command, "relaxon", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "workoff", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDirect(WorkRelaxMode.Relax, tokens, out mode, out error);
        }

        if (!string.Equals(command, "mode", StringComparison.OrdinalIgnoreCase))
        {
            error = "Unknown Work / Relax command.";
            return false;
        }

        if (tokens == null || tokens.Count != 1)
        {
            error = "Use /mode work or /mode relax.";
            return false;
        }

        string requested = (tokens[0] ?? string.Empty).Trim();
        if (string.Equals(requested, "work", StringComparison.OrdinalIgnoreCase))
        {
            mode = WorkRelaxMode.Work;
            return true;
        }

        if (string.Equals(requested, "relax", StringComparison.OrdinalIgnoreCase))
        {
            mode = WorkRelaxMode.Relax;
            return true;
        }

        error = "Use /mode work or /mode relax.";
        return false;
    }

    private static bool ResolveDirect(
        WorkRelaxMode resolved,
        IReadOnlyList<string> tokens,
        out WorkRelaxMode mode,
        out string error)
    {
        mode = default;
        if (tokens != null && tokens.Count > 0)
        {
            error = "This command does not accept arguments.";
            return false;
        }

        mode = resolved;
        error = string.Empty;
        return true;
    }

    private static void Register(
        BlueSageCommandRegistry registry,
        string commandName,
        string description,
        Func<WorkRelaxMode?> readMode,
        Func<WorkRelaxMode, bool> setMode)
    {
        registry.Register(new BlueSageCommand(
            commandName,
            description,
            Array.Empty<string>(),
            context => Execute(context, readMode, setMode)));
    }

    private static BlueSageCommandResult Execute(
        BlueSageCommandContext context,
        Func<WorkRelaxMode?> readMode,
        Func<WorkRelaxMode, bool> setMode)
    {
        if (!TryResolve(context.CommandName, context.Tokens, out WorkRelaxMode requested, out string error))
        {
            return BlueSageCommandResult.HandledWithMessages(error);
        }

        WorkRelaxMode? current = readMode();
        if (current.HasValue && current.Value == requested)
        {
            return BlueSageCommandResult.HandledWithMessages();
        }

        if (!setMode(requested))
        {
            return BlueSageCommandResult.HandledWithMessages("Mode is waiting for local statistics.");
        }

        return BlueSageCommandResult.HandledWithMessages("Mode: " + requested + ".");
    }
}
