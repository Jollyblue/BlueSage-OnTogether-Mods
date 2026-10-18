namespace BlueSage.QoLTweaks.Core;

internal static class MiniMapFriendVisualPolicy
{
    public static bool ShouldMoveToLastSibling(
        bool isFriend,
        int currentSiblingIndex,
        int siblingCount)
    {
        return isFriend &&
               siblingCount > 0 &&
               currentSiblingIndex != siblingCount - 1;
    }
}
