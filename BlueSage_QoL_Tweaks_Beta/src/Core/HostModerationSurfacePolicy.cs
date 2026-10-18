namespace BlueSage.QoLTweaks.Core;

internal readonly struct HostModerationSurfaceDecision
{
    internal HostModerationSurfaceDecision(
        bool canInspectNativeActions,
        bool canArmHostRemove,
        bool canExecuteHostRemove)
    {
        CanInspectNativeActions = canInspectNativeActions;
        CanArmHostRemove = canArmHostRemove;
        CanExecuteHostRemove = canExecuteHostRemove;
    }

    internal bool CanInspectNativeActions { get; }
    internal bool CanArmHostRemove { get; }
    internal bool CanExecuteHostRemove { get; }
}

internal static class HostModerationSurfacePolicy
{
    internal static HostModerationSurfaceDecision Evaluate(
        bool isHost,
        bool exactTargetReady,
        bool isSelf,
        bool confirmationArmed)
    {
        bool hostRemoveReady =
            isHost && exactTargetReady && !isSelf;
        return new HostModerationSurfaceDecision(
            canInspectNativeActions: exactTargetReady,
            canArmHostRemove: hostRemoveReady,
            canExecuteHostRemove:
                hostRemoveReady && confirmationArmed);
    }
}
