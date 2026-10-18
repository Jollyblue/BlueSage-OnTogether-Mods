using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CompletionAwareSweepContractTests
{
    [Test]
    public void Sweep_is_single_flight_and_logs_only_after_unload_completion()
    {
        string source = ReadPluginSource();

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("_sweepOperationGate.TryStart(reason, out int sequence)"));
            Assert.That(source, Does.Contain("operation = Resources.UnloadUnusedAssets();"));
            Assert.That(source, Does.Contain("yield return operation;"));
            Assert.That(source, Does.Contain("phase=start"));
            Assert.That(source, Does.Contain("phase=complete"));
            Assert.That(source, Does.Contain("phase=skip"));
            Assert.That(source, Does.Not.Contain("Log.LogInfo($\"Running asset sweep via {reason}: Resources.UnloadUnusedAssets().\");\n        Resources.UnloadUnusedAssets();"));
        });
    }

    [Test]
    public void Sweep_completion_always_logs_and_offers_an_opt_in_chat_notification()
    {
        string source = ReadPluginSource();
        string completion = Slice(
            source,
            "private IEnumerator CompleteSweepOperation(int sequence, string reason)",
            "private void FinishSweepOperation(int sequence, string reason)");

        Assert.Multiple(() =>
        {
            Assert.That(completion, Does.Contain("Log.LogInfo($\"[BlueSageSweep]"));
            Assert.That(completion, Does.Contain("if (NotifyOnSweep.Value)"));
            Assert.That(completion, Does.Contain("AddLocalNotification(\"BlueSage QoL swept unused assets.\")"));
            Assert.That(source, Does.Contain("NotifyOnSweep = Config.Bind(\"Asset Sweep\", \"NotifyOnSweep\", true,"));
        });
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate BlueSage QoL project root.");
    }

    private static string ReadPluginSource()
    {
        return File.ReadAllText(Path.Combine(FindProjectRoot(), "src", "Plugin.cs"))
            .Replace("\r\n", "\n");
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), "Start marker missing: " + startMarker);
        int end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), "End marker missing: " + endMarker);
        return source.Substring(start, end - start);
    }
}
