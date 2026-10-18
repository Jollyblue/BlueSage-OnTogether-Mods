using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueSage.QoLTweaks.Core;

internal static class AuthoritativePlayerRowReconciliationPolicy
{
    internal static int Reconcile(
        bool authoritativeCoverageComplete,
        IReadOnlyCollection<int> currentRowInstanceIds,
        IEnumerable<int> ownedRowInstanceIds,
        Action<int> pruneOwnedRow)
    {
        if (!authoritativeCoverageComplete ||
            currentRowInstanceIds == null ||
            currentRowInstanceIds.Count == 0 ||
            ownedRowInstanceIds == null ||
            pruneOwnedRow == null)
        {
            return 0;
        }

        var currentRows = new HashSet<int>();
        foreach (int rowInstanceId in currentRowInstanceIds)
        {
            if (rowInstanceId == 0 ||
                !currentRows.Add(rowInstanceId))
            {
                return 0;
            }
        }

        int[] staleRows = ownedRowInstanceIds
            .Where(rowInstanceId =>
                rowInstanceId != 0 &&
                !currentRows.Contains(rowInstanceId))
            .Distinct()
            .ToArray();
        foreach (int staleRow in staleRows)
        {
            pruneOwnedRow(staleRow);
        }
        return staleRows.Length;
    }
}
