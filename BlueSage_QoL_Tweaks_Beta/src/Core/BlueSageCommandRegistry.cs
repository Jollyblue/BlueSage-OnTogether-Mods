#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal sealed class BlueSageCommand
{
    public BlueSageCommand(string name, string description, IEnumerable<string> aliases, Func<BlueSageCommandContext, BlueSageCommandResult> handler, bool isHidden = false)
    {
        Name = NormalizeName(name);
        Description = description ?? string.Empty;
        Aliases = (aliases ?? Enumerable.Empty<string>())
            .Select(NormalizeName)
            .Where(alias => alias.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        IsHidden = isHidden;
    }

    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<string> Aliases { get; }
    public Func<BlueSageCommandContext, BlueSageCommandResult> Handler { get; }
    public bool IsHidden { get; }

    public bool Matches(string commandName)
    {
        string normalized = NormalizeName(commandName);
        return string.Equals(Name, normalized, StringComparison.OrdinalIgnoreCase) ||
               Aliases.Any(alias => string.Equals(alias, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public bool CouldMatch(string commandPrefix)
    {
        string normalized = NormalizeName(commandPrefix);
        return Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase) ||
               Aliases.Any(alias => alias.StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeName(string value)
    {
        return (value ?? string.Empty).Trim().TrimStart('/').ToLowerInvariant();
    }
}

internal sealed class BlueSageCommandContext
{
    public BlueSageCommandContext(string rawInput, string commandName, string arguments)
    {
        RawInput = rawInput ?? string.Empty;
        CommandName = commandName ?? string.Empty;
        Arguments = arguments ?? string.Empty;
        Tokens = Arguments
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
    }

    public string RawInput { get; }
    public string CommandName { get; }
    public string Arguments { get; }
    public IReadOnlyList<string> Tokens { get; }
}

internal sealed class BlueSageCommandResult
{
    private BlueSageCommandResult(bool handled, IEnumerable<string> messages)
    {
        Handled = handled;
        Messages = (messages ?? Enumerable.Empty<string>())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray();
    }

    public bool Handled { get; }
    public IReadOnlyList<string> Messages { get; }

    public static BlueSageCommandResult Unhandled()
    {
        return new BlueSageCommandResult(false, Array.Empty<string>());
    }

    public static BlueSageCommandResult HandledWithMessages(params string[] messages)
    {
        return new BlueSageCommandResult(true, messages);
    }
}

internal sealed class BlueSageCommandRegistry
{
    private readonly List<BlueSageCommand> _commands = new List<BlueSageCommand>();

    public IReadOnlyList<BlueSageCommand> Commands => _commands;

    public void Register(BlueSageCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (_commands.Any(existing => existing.Matches(command.Name)))
        {
            throw new InvalidOperationException($"BlueSage command already registered: /{command.Name}");
        }

        _commands.Add(command);
    }

    public BlueSageCommandResult TryExecute(string input)
    {
        if (!TryParse(input, out string commandName, out string arguments))
        {
            return BlueSageCommandResult.Unhandled();
        }

        var command = _commands.FirstOrDefault(candidate => candidate.Matches(commandName));
        if (command == null)
        {
            return BlueSageCommandResult.Unhandled();
        }

        return command.Handler(new BlueSageCommandContext(input, commandName, arguments));
    }

    public IReadOnlyList<string> Suggest(string input, int maxResults, Func<BlueSageCommand, bool>? include = null)
    {
        if (maxResults < 1)
        {
            return Array.Empty<string>();
        }

        string trimmed = input?.TrimStart() ?? string.Empty;
        if (!trimmed.StartsWith("/", StringComparison.Ordinal) &&
            !trimmed.StartsWith("./", StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        string prefix = TrimCommandPrefix(trimmed).Split(new[] { ' ' }, 2)[0];
        return _commands
            .Where(command => !command.IsHidden &&
                              (include == null || include(command)) &&
                              command.CouldMatch(prefix))
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(command => "/" + command.Name + " - " + command.Description)
            .ToArray();
    }

    internal static bool TryParse(string input, out string commandName, out string arguments)
    {
        commandName = string.Empty;
        arguments = string.Empty;

        string trimmed = (input ?? string.Empty).Trim();
        if ((!trimmed.StartsWith("/", StringComparison.Ordinal) && !trimmed.StartsWith("./", StringComparison.Ordinal)) ||
            trimmed == "/" ||
            trimmed == "./")
        {
            return false;
        }

        string withoutSlash = TrimCommandPrefix(trimmed);
        int firstSpace = withoutSlash.IndexOf(' ');
        if (firstSpace < 0)
        {
            commandName = withoutSlash;
            return true;
        }

        commandName = withoutSlash.Substring(0, firstSpace);
        arguments = withoutSlash.Substring(firstSpace + 1).Trim();
        return commandName.Length > 0;
    }

    private static string TrimCommandPrefix(string input)
    {
        return input.StartsWith("./", StringComparison.Ordinal) ? input.Substring(2) : input.Substring(1);
    }
}
