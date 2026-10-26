using System;
using System.IO;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

// Round 4 Stage G (Marbles, 2026-10-20): nothing of ours may do file work inside the game's autosave frame.
// The SavePlayerStatistics postfix marks the ledger dirty; Plugin.Update flushes it on a later frame; the store
// serializes and writes a deep-cloned snapshot on a worker thread; only the shutdown save syncs to disk.
public sealed class WorkRelaxAutosaveFrameContractTests
{
    [Test]
    public void Reconcile_after_native_save_marks_dirty_and_never_saves_inline()
    {
        string runtime = ReadSource("src", "WorkRelaxRuntimeController.cs");
        string reconcile = MethodBody(runtime, "internal static void ReconcileAfterNativeSave()");

        Assert.Multiple(() =>
        {
            Assert.That(reconcile, Does.Contain("MarkDirty("));
            Assert.That(reconcile, Does.Not.Contain("SaveFailOpen("));
            Assert.That(reconcile, Does.Not.Contain("_store"));
        });
    }

    [Test]
    public void Game_hooks_never_initialize_the_ledger_and_plugin_update_retries_it()
    {
        string runtime = ReadSource("src", "WorkRelaxRuntimeController.cs");
        string plugin = ReadSource("src", "Plugin.cs");

        Assert.Multiple(() =>
        {
            foreach (string signature in new[]
                     {
                         "internal static void ReconcileAfterNativeSave()",
                         "internal static WorkRelaxObservationState? BeginObservation(",
                         "internal static void ObservePomodoroState(bool isStudy, bool isPaused)",
                         "internal static void BeginActivityInterval(string focusTypeName)"
                     })
            {
                Assert.That(MethodBody(runtime, signature), Does.Not.Contain("TryInitialize()"), signature + " runs inside a game hook; the one-time ledger load must not happen there");
            }

            Assert.That(runtime, Does.Contain("internal static void TryInitializeIfDue(long nowUtcTicks)"));
            Assert.That(plugin, Does.Contain("WorkRelaxRuntimeController.TryInitializeIfDue(DateTime.UtcNow.Ticks);"));
        });
    }

    [Test]
    public void Plugin_update_flushes_the_ledger_on_a_later_frame()
    {
        string plugin = ReadSource("src", "Plugin.cs");
        string runtime = ReadSource("src", "WorkRelaxRuntimeController.cs");

        Assert.Multiple(() =>
        {
            Assert.That(plugin, Does.Contain("WorkRelaxRuntimeController.FlushIfDue(DateTime.UtcNow.Ticks);"));
            Assert.That(runtime, Does.Contain("WorkRelaxLedgerSavePolicy.IsDue("));
            Assert.That(runtime, Does.Contain("_store.SaveInBackground("));
            Assert.That(runtime, Does.Contain("_ledger.Clone()"), "the worker thread serializes a deep clone, never the live ledger");
        });
    }

    [Test]
    public void Only_the_shutdown_save_syncs_to_disk_and_it_waits_for_the_worker()
    {
        string store = ReadSource("src", "WorkRelaxLedgerStore.cs");
        string runtime = ReadSource("src", "WorkRelaxRuntimeController.cs");
        string flush = MethodBody(runtime, "internal static void Flush()");

        Assert.Multiple(() =>
        {
            Assert.That(CountOf(store, "Flush(true)"), Is.EqualTo(1), "exactly one fsync path: the synchronous Save used at shutdown");
            Assert.That(store, Does.Contain("ThreadPool.QueueUserWorkItem("));
            Assert.That(store, Does.Contain("internal bool SaveInBackground("));
            Assert.That(store, Does.Contain("internal bool WaitForBackgroundWrite("));
            Assert.That(flush, Does.Contain("WaitForBackgroundWrite("));
            Assert.That(flush, Does.Contain("SaveFailOpen(\"shutdown\")"));
        });
    }

    [Test]
    public void Codec_reuses_one_serializer()
    {
        string codec = ReadSource("src", "Core", "WorkRelaxLedgerCodec.cs");
        Assert.Multiple(() =>
        {
            Assert.That(codec, Does.Contain("private static readonly DataContractJsonSerializer"));
            Assert.That(codec, Does.Not.Contain("return new DataContractJsonSerializer(typeof(WorkRelaxLedger));"));
        });
    }

    private static string MethodBody(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), signature);
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            if (source[index] == '}' && --depth == 0)
            {
                return source.Substring(open, index - open + 1);
            }
        }

        throw new InvalidOperationException("Unbalanced method body for " + signature);
    }

    private static int CountOf(string source, string needle) =>
        source.Split(new[] { needle }, StringSplitOptions.None).Length - 1;

    private static string ReadSource(params string[] pathParts)
    {
        string path = FindProjectRoot();
        foreach (string part in pathParts)
        {
            path = Path.Combine(path, part);
        }

        return File.ReadAllText(path);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "BlueSage_QoL_Tweaks_Beta.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find QoL project root.");
    }
}
