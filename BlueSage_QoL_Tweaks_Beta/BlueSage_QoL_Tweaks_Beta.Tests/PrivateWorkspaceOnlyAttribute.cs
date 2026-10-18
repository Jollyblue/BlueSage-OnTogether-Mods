using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace BlueSage.QoLTweaks.Tests;

// Marks a test that reads files only the private BlueSage workspace has (release custody, the private
// companion builds, internal docs and tools). Inside the private workspace the test always runs, so a
// missing file there still fails loudly. In the public repo, where those folders never exist, the test
// is reported as ignored instead of failing on a file that was never meant to be published.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PrivateWorkspaceOnlyAttribute : NUnitAttribute, IApplyToTest
{
    internal const string MarkerFolder = "BlueSage_OnTogether_ModArmory_Private";

    public void ApplyToTest(NUnit.Framework.Internal.Test test)
    {
        if (test.RunState == RunState.NotRunnable || IsPrivateWorkspace())
        {
            return;
        }

        test.RunState = RunState.Ignored;
        test.Properties.Set(PropertyNames.SkipReason, "Private workspace only: reads files that are not part of the public repo.");
    }

    internal static bool IsPrivateWorkspace()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, MarkerFolder)))
            {
                return true;
            }

            directory = directory.Parent;
        }

        return false;
    }
}
