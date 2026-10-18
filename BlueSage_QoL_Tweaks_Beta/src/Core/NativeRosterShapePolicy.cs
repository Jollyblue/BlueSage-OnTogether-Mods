namespace BlueSage.QoLTweaks.Core;

internal static class NativeRosterShapePolicy
{
    internal static bool HasAlignedActivePrefix(
        int steamIdCount,
        int playerIdCount,
        int transformCount,
        int idInfoCount,
        int pooledRowCount)
    {
        return steamIdCount >= 0 &&
               playerIdCount == steamIdCount &&
               transformCount == steamIdCount &&
               idInfoCount == steamIdCount &&
               pooledRowCount >= steamIdCount;
    }
}
