#nullable enable

using System.Globalization;

namespace BlueSage.QoLTweaks.Core;

internal static class PurrNetPlayerIdPolicy
{
    internal static string Format(ulong value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
