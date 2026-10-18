using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal static class InputFieldLimitPolicy
{
    // 2026-10-17 (Blue): chat defaults to the maximum. The warning threshold stays separate:
    // past 3000, rich-text-heavy lines can wrap or clip in some game UI.
    public const int DefaultMaxChatCharacters = 4000;
    public const int ChatWrapWarningCharacters = 3000;
    public const int DefaultMaxProfileCharacters = 4000;
    public const int DefaultMaxSessionNameCharacters = 4000;
    public const int MinChatCharacters = 1;
    public const int AbsoluteMaxChatCharacters = 4000;
    public const int AbsoluteMaxProfileCharacters = 4000;
    public const int AbsoluteMaxSessionNameCharacters = 4000;

    private static readonly HashSet<string> SessionNameFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "InputField_SesionName",
        "InputField_SessionName",
    };

    private static readonly HashSet<string> IdCardFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "InputField_Name",
        "InputField_DateOfBirth",
        "InputField_HoroscopeSign",
        "InputField_Location",
        "InputField_AreaOfStudy",
        "InputField_FavoriteQuote",
        "NameInputField",
        "DateOfBirthInputField",
        "HoroscopeSignInputField",
        "LocationInputField",
        "AreaOfStudyInputField",
        "FavoriteQuoteInputField",
    };

    private static readonly HashSet<string> ChatInputFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "InputField_Chat",
        "InputField_Message",
        "InputField_ChatMessage",
        "ChatInputField",
        "MessageInputField",
    };

    public static int? GetLimit(
        string fieldName,
        int maxIdCardCharacters,
        int maxSessionNameCharacters,
        int maxChatCharacters)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return null;
        }

        if (IdCardFields.Contains(fieldName))
        {
            return ClampProfileCharacters(maxIdCardCharacters);
        }

        if (SessionNameFields.Contains(fieldName))
        {
            return ClampSessionNameCharacters(maxSessionNameCharacters);
        }

        if (LooksLikeChatInput(fieldName))
        {
            return ClampChatCharacters(maxChatCharacters);
        }

        return null;
    }

    public static int ClampChatCharacters(int value)
    {
        return Clamp(value, MinChatCharacters, AbsoluteMaxChatCharacters);
    }

    public static int ClampProfileCharacters(int value)
    {
        return Clamp(value, 1, AbsoluteMaxProfileCharacters);
    }

    public static int ClampSessionNameCharacters(int value)
    {
        return Clamp(value, 1, AbsoluteMaxSessionNameCharacters);
    }

    public static int ResolveCreateSessionNameLimit(int currentCharacterLimit, int configuredSessionNameCharacters)
    {
        _ = currentCharacterLimit;
        return ClampSessionNameCharacters(configuredSessionNameCharacters);
    }

    private static bool LooksLikeChatInput(string fieldName)
    {
        if (ChatInputFields.Contains(fieldName))
        {
            return true;
        }

        return fieldName.IndexOf("chat", StringComparison.OrdinalIgnoreCase) >= 0 ||
               fieldName.IndexOf("message", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }
}
