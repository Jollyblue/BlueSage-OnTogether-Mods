namespace BlueSage.QoLTweaks.Core;

internal enum CreatorHostLabelHeartbeatDecision
{
    None,
    Publish,
    ClearAndStop,
}

internal static class CreatorHostLabelPublicationPolicy
{
    internal static bool CanPublish(
        bool exactOwnedHost,
        bool exactIdentityGenerationFresh,
        bool creatorContributionFresh,
        bool privateHostAuthorityFresh = false)
    {
        return exactOwnedHost &&
               exactIdentityGenerationFresh &&
               (creatorContributionFresh ||
                privateHostAuthorityFresh);
    }

    internal static CreatorHostLabelHeartbeatDecision DecideHeartbeat(
        bool labelActive,
        bool heartbeatDue,
        bool exactOwnedHost,
        bool exactIdentityGenerationFresh,
        bool creatorContributionFresh,
        bool privateHostAuthorityFresh = false)
    {
        if (!labelActive || !heartbeatDue)
        {
            return CreatorHostLabelHeartbeatDecision.None;
        }

        return CanPublish(
            exactOwnedHost,
            exactIdentityGenerationFresh,
            creatorContributionFresh,
            privateHostAuthorityFresh)
            ? CreatorHostLabelHeartbeatDecision.Publish
            : CreatorHostLabelHeartbeatDecision.ClearAndStop;
    }
}

internal static class PlayerHostLabelDisplayPolicy
{
    internal static bool TryResolve(
        string payload,
        long nowUnixSeconds,
        bool isExactCurrentOwner,
        out string label)
    {
        label = string.Empty;
        if (!isExactCurrentOwner ||
            !RoleLabelProtocolPolicy.TryParse(
                payload,
                nowUnixSeconds,
                out RoleLabelProtocolMessage message,
                out _) ||
            message.Action != RoleLabelProtocolAction.Set ||
            string.IsNullOrWhiteSpace(message.Label))
        {
            return false;
        }

        label = message.Label;
        return true;
    }

    internal static bool TrySuppressNativeHostSuffix(
        string renderedName,
        string nativeHostLabel,
        bool uniqueExactRow,
        bool ownerMatchesRow,
        bool nativeHostAgreement,
        bool customHostLabelActive,
        out string replacement)
    {
        string cleanHostLabel = (nativeHostLabel ?? string.Empty).Trim();
        string suffix = string.IsNullOrWhiteSpace(cleanHostLabel)
            ? string.Empty
            : " " + cleanHostLabel;

        replacement = string.Empty;
        if (!uniqueExactRow ||
            !ownerMatchesRow ||
            !nativeHostAgreement ||
            !customHostLabelActive ||
            string.IsNullOrWhiteSpace(suffix) ||
            string.IsNullOrWhiteSpace(renderedName) ||
            !renderedName.EndsWith(
                suffix,
                System.StringComparison.Ordinal))
        {
            return false;
        }

        replacement = renderedName.Substring(
            0,
            renderedName.Length - suffix.Length);
        return !string.IsNullOrWhiteSpace(replacement);
    }

    internal static bool TrySuppressNativeHostSuffix(
        string renderedName,
        bool uniqueExactRow,
        bool ownerMatchesRow,
        bool nativeHostAgreement,
        bool creatorContributionFresh,
        bool customHostLabelActive,
        out string replacement)
    {
        replacement = string.Empty;
        return creatorContributionFresh &&
            TrySuppressNativeHostSuffix(
                renderedName,
                "(Host)",
                uniqueExactRow,
                ownerMatchesRow,
                nativeHostAgreement,
                customHostLabelActive,
                out replacement);
    }
}
