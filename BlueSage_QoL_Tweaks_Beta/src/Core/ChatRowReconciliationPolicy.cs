using System.Collections.Generic;

namespace BlueSage.QoLTweaks.Core;

internal readonly struct ChatRowReconciliationDecision
{
    internal ChatRowReconciliationDecision(
        bool hasExactCreatedRow,
        int createdRowInstanceId)
    {
        HasExactCreatedRow = hasExactCreatedRow;
        CreatedRowInstanceId = createdRowInstanceId;
    }

    internal bool HasExactCreatedRow { get; }

    internal int CreatedRowInstanceId { get; }
}

internal static class ChatRowReconciliationPolicy
{
    internal static ChatRowReconciliationDecision Resolve(
        IReadOnlyList<int> beforeInstanceIds,
        IReadOnlyList<int> afterInstanceIds)
    {
        if (afterInstanceIds == null || afterInstanceIds.Count == 0)
        {
            return default;
        }

        HashSet<int> existing = new HashSet<int>();
        if (beforeInstanceIds != null)
        {
            for (int index = 0; index < beforeInstanceIds.Count; index++)
            {
                existing.Add(beforeInstanceIds[index]);
            }
        }

        int createdId = 0;
        int createdCount = 0;
        for (int index = 0; index < afterInstanceIds.Count; index++)
        {
            int candidateId = afterInstanceIds[index];
            if (existing.Contains(candidateId))
            {
                continue;
            }

            createdId = candidateId;
            createdCount++;
            if (createdCount > 1)
            {
                return default;
            }
        }

        return createdCount == 1
            ? new ChatRowReconciliationDecision(true, createdId)
            : default;
    }
}
