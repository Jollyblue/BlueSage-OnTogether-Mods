using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BlueSage.QoLTweaks.Core;

namespace BlueSage.QoLTweaks;

// Round 4 WS1: durable storage for avatar slots 4-9. The new content is written to a temp file first, the
// previous versions rotate into .bak1-.bak3, and the temp file replaces the main file in one step, so a crash
// or forced close mid-save can never leave every slot empty. Loading takes the newest complete file.
internal static class AvatarSlotStore
{
    private const int BackupCount = 3;

    internal static string[] FilesInTrustOrder(string mainPath)
    {
        string[] files = new string[BackupCount + 1];
        files[0] = mainPath;
        for (int index = 1; index <= BackupCount; index++)
        {
            files[index] = mainPath + ".bak" + index;
        }

        return files;
    }

    // True when the main file or any backup exists, readable or not: there is data on disk worth protecting.
    internal static bool AnyFileExists(string mainPath)
    {
        foreach (string file in FilesInTrustOrder(mainPath))
        {
            if (File.Exists(file))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadOrNull(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static bool TryLoad(string mainPath, int slotCount, out int selectedSlot, out string[] names, out string[] payloads, out string usedFile)
    {
        return TryLoad(mainPath, slotCount, out selectedSlot, out _, out names, out payloads, out usedFile);
    }

    internal static bool TryLoad(string mainPath, int slotCount, out int selectedSlot, out string legacyConfigHash, out string[] names, out string[] payloads, out string usedFile)
    {
        selectedSlot = -1;
        legacyConfigHash = null;
        names = new string[slotCount];
        payloads = new string[slotCount];
        usedFile = null;

        string[] files = FilesInTrustOrder(mainPath);
        string[] contents = new string[files.Length];
        for (int index = 0; index < files.Length; index++)
        {
            contents[index] = ReadOrNull(files[index]);
        }

        int chosen = AvatarSlotStoreFormat.ChooseFirstValid(contents, slotCount);
        if (chosen < 0)
        {
            return false;
        }

        usedFile = files[chosen];
        return AvatarSlotStoreFormat.TryParse(contents[chosen], slotCount, out selectedSlot, out legacyConfigHash, out names, out payloads);
    }

    // Review R2: before a changed .cfg replaces the slots, the file the session loaded is copied to a dated
    // ".pre-reimport-" sibling. It sits outside the trust chain, so it is never loaded or rotated, only kept.
    internal static string BackupBeforeReimport(string mainPath, string loadedFile, DateTime stamp)
    {
        string copy = mainPath + ".pre-reimport-" + stamp.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        File.Copy(loadedFile, copy, overwrite: true);
        return copy;
    }

    internal static void Save(string mainPath, int selectedSlot, IReadOnlyList<string> names, IReadOnlyList<string> payloads)
    {
        Save(mainPath, selectedSlot, null, names, payloads);
    }

    internal static void Save(string mainPath, int selectedSlot, string legacyConfigHash, IReadOnlyList<string> names, IReadOnlyList<string> payloads)
    {
        string directory = Path.GetDirectoryName(mainPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Unchanged content is not saved again, so repeated saves never push copies of it down the backup chain.
        string text = AvatarSlotStoreFormat.Format(selectedSlot, legacyConfigHash, names, payloads);
        string current = ReadOrNull(mainPath);
        if (string.Equals(current, text, StringComparison.Ordinal))
        {
            return;
        }

        string tempPath = mainPath + ".tmp";
        WriteToDisk(tempPath, text);

        // Backups rotate by rename, oldest first, and only a complete file rotates: a damaged file can never
        // overwrite a good backup, and nothing is rewritten, so a power loss mid-save touches at most one file.
        // The last step (main -> backup 1) is done by File.Replace below, in the same call that swaps the new file in.
        string mainBackup = null;
        foreach (KeyValuePair<string, string> step in AvatarSlotStoreFormat.RotationPlan(FilesInTrustOrder(mainPath)))
        {
            if (string.Equals(step.Key, mainPath, StringComparison.Ordinal))
            {
                mainBackup = step.Value;
                continue;
            }

            if (AvatarSlotStoreFormat.TryParse(ReadOrNull(step.Key), names.Count, out _, out _, out _))
            {
                MoveOver(step.Key, step.Value);
            }
        }

        if (!File.Exists(mainPath))
        {
            File.Move(tempPath, mainPath);
            return;
        }

        // A complete main file becomes backup 1; a damaged one is discarded (null backup) rather than rotated.
        bool mainComplete = AvatarSlotStoreFormat.TryParse(current, names.Count, out _, out _, out _);
        File.Replace(tempPath, mainPath, mainComplete ? mainBackup : null);
    }

    // FileStream.Flush(true) asks the OS to write the bytes through to the disk before the swap; a plain write
    // only reaches the OS cache, which a power loss discards.
    private static void WriteToDisk(string path, string text)
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
    }

    // net46 has no File.Move overwrite flag: the destination is removed first. The source is the newer file.
    private static void MoveOver(string source, string destination)
    {
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(source, destination);
    }
}
