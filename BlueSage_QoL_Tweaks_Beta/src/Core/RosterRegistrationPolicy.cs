#nullable disable

using System;
using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal enum RosterRegistrationAction
{
    RejectInvalid,
    IgnoreExactDuplicate,
    ReplaceStableIdentity,
    Append,
}

internal readonly struct RosterRegistrationDecision
{
    internal RosterRegistrationDecision(RosterRegistrationAction action, int existingIndex)
    {
        Action = action;
        ExistingIndex = existingIndex;
    }

    internal RosterRegistrationAction Action { get; }
    internal int ExistingIndex { get; }
}

internal static class RosterRegistrationPolicy
{
    internal static RosterRegistrationDecision Evaluate<TTransform>(
        string incomingSteamId,
        string incomingVivoxId,
        TTransform incomingTransform,
        IReadOnlyList<string> existingSteamIds,
        IReadOnlyList<string> existingVivoxIds,
        IReadOnlyList<TTransform> existingTransforms)
        where TTransform : class
    {
        if (string.IsNullOrWhiteSpace(incomingSteamId) || ReferenceEquals(incomingTransform, null))
        {
            return new RosterRegistrationDecision(RosterRegistrationAction.RejectInvalid, -1);
        }

        if (existingTransforms != null)
        {
            for (int index = 0; index < existingTransforms.Count; index++)
            {
                if (ReferenceEquals(existingTransforms[index], incomingTransform))
                {
                    return new RosterRegistrationDecision(RosterRegistrationAction.IgnoreExactDuplicate, index);
                }
            }
        }

        int steamCount = existingSteamIds?.Count ?? 0;
        for (int index = 0; index < steamCount; index++)
        {
            if (string.Equals(existingSteamIds[index], incomingSteamId, StringComparison.Ordinal))
            {
                return new RosterRegistrationDecision(RosterRegistrationAction.ReplaceStableIdentity, index);
            }
        }

        int vivoxCount = existingVivoxIds?.Count ?? 0;
        if (!string.IsNullOrWhiteSpace(incomingVivoxId))
        {
            for (int index = 0; index < vivoxCount; index++)
            {
                if (string.Equals(existingVivoxIds[index], incomingVivoxId, StringComparison.Ordinal))
                {
                    return new RosterRegistrationDecision(RosterRegistrationAction.ReplaceStableIdentity, index);
                }
            }
        }

        return new RosterRegistrationDecision(RosterRegistrationAction.Append, -1);
    }
}
