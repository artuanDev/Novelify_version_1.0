using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Novelify
{
    /// <summary>Atomic JSON slot storage. Snapshot validation remains the session's responsibility.</summary>
    public sealed class NovelSaveStorage
    {
        public const string StoryExtension = ".novelsave";
        public const string ProfileFileName = "profile.novelprofile";
        public string SaveDirectory { get; }

        public NovelSaveStorage(string saveDirectory = null)
        {
            SaveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
                ? Path.Combine(Application.persistentDataPath, "Novelify")
                : Path.GetFullPath(saveDirectory);
        }

        public NovelPersistenceResult SaveSlot(string slotID, NovelSaveData snapshot)
        {
            if (!TryGetSlotPath(slotID, out string path, out NovelPersistenceResult invalid)) return invalid;
            if (snapshot == null) return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Snapshot is null.");
            return WriteAtomic(path, snapshot);
        }

        public NovelPersistenceResult LoadSlot(string slotID, out NovelSaveData snapshot)
        {
            snapshot = null;
            if (!TryGetSlotPath(slotID, out string path, out NovelPersistenceResult invalid)) return invalid;
            return ReadWithBackup(path, out snapshot);
        }

        public NovelPersistenceResult SaveProfile(NovelProfileSaveData profile) =>
            profile == null
                ? new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Profile is null.")
                : WriteAtomic(Path.Combine(SaveDirectory, ProfileFileName), profile);

        public NovelPersistenceResult LoadProfile(out NovelProfileSaveData profile) =>
            ReadWithBackup(Path.Combine(SaveDirectory, ProfileFileName), out profile);

        public IReadOnlyList<string> ListSlots()
        {
            if (!Directory.Exists(SaveDirectory)) return Array.Empty<string>();
            return Directory.GetFiles(SaveDirectory, "*" + StoryExtension, SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(IsValidSlotID)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public NovelPersistenceResult DeleteSlot(string slotID)
        {
            if (!TryGetSlotPath(slotID, out string path, out NovelPersistenceResult invalid)) return invalid;
            try
            {
                if (!File.Exists(path) && !File.Exists(path + ".bak"))
                    return new NovelPersistenceResult(NovelPersistenceStatus.NotFound, $"Save slot '{slotID}' does not exist.");
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return new NovelPersistenceResult(NovelPersistenceStatus.IOError, exception.Message);
            }
        }

        public string GetSlotPath(string slotID) =>
            TryGetSlotPath(slotID, out string path, out _) ? path : null;

        public static bool IsValidSlotID(string slotID)
        {
            if (string.IsNullOrWhiteSpace(slotID) || slotID.Length > 64) return false;
            foreach (char character in slotID)
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_') return false;
            return true;
        }

        private bool TryGetSlotPath(string slotID, out string path, out NovelPersistenceResult result)
        {
            path = null;
            if (!IsValidSlotID(slotID))
            {
                result = new NovelPersistenceResult(NovelPersistenceStatus.InvalidSlot,
                    "Slot IDs may contain only letters, numbers, '-' and '_', up to 64 characters.");
                return false;
            }
            path = Path.Combine(SaveDirectory, slotID + StoryExtension);
            result = new NovelPersistenceResult(NovelPersistenceStatus.Success);
            return true;
        }

        private static NovelPersistenceResult WriteAtomic<T>(string path, T data) where T : class
        {
            string temporary = path + ".tmp";
            string backup = path + ".bak";
            try
            {
                string payload = JsonUtility.ToJson(data, false);
                var envelope = new NovelSaveEnvelope { Payload = payload, Checksum = ComputeChecksum(payload) };
                string json = JsonUtility.ToJson(envelope, true);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                File.WriteAllText(temporary, json, new UTF8Encoding(false));

                // Read the temporary file back before it can replace a known-good slot.
                NovelPersistenceResult verification = ReadFile(temporary, out T _);
                if (!verification.Succeeded) return verification;

                if (File.Exists(path))
                {
                    try { File.Replace(temporary, path, backup, true); }
                    catch (PlatformNotSupportedException) { ReplaceFallback(temporary, path, backup); }
                    catch (IOException) { ReplaceFallback(temporary, path, backup); }
                }
                else File.Move(temporary, path);
                return new NovelPersistenceResult(NovelPersistenceStatus.Success);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is CryptographicException)
            {
                return new NovelPersistenceResult(NovelPersistenceStatus.IOError, exception.Message);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
            }
        }

        private static void ReplaceFallback(string temporary, string path, string backup)
        {
            File.Copy(path, backup, true);
            File.Delete(path);
            File.Move(temporary, path);
        }

        private static NovelPersistenceResult ReadWithBackup<T>(string path, out T data) where T : class
        {
            data = null;
            if (!File.Exists(path)) return new NovelPersistenceResult(NovelPersistenceStatus.NotFound, "Save file was not found.");
            NovelPersistenceResult primary = ReadFile(path, out data);
            if (primary.Succeeded) return primary;

            string backup = path + ".bak";
            if (!File.Exists(backup)) return primary;
            NovelPersistenceResult backupResult = ReadFile(backup, out data);
            return backupResult.Succeeded
                ? new NovelPersistenceResult(NovelPersistenceStatus.RestoredBackup,
                    "The active save was corrupt; the last-good backup was loaded.")
                : primary;
        }

        private static NovelPersistenceResult ReadFile<T>(string path, out T data) where T : class
        {
            data = null;
            try
            {
                NovelSaveEnvelope envelope = JsonUtility.FromJson<NovelSaveEnvelope>(File.ReadAllText(path, Encoding.UTF8));
                if (envelope == null || envelope.EnvelopeVersion != 1 || string.IsNullOrEmpty(envelope.Payload) ||
                    !string.Equals(envelope.Checksum, ComputeChecksum(envelope.Payload), StringComparison.OrdinalIgnoreCase))
                    return new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Save checksum or envelope is invalid.");
                data = JsonUtility.FromJson<T>(envelope.Payload);
                return data != null
                    ? new NovelPersistenceResult(NovelPersistenceStatus.Success)
                    : new NovelPersistenceResult(NovelPersistenceStatus.Corrupt, "Save payload could not be deserialized.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                               exception is ArgumentException || exception is CryptographicException)
            {
                return new NovelPersistenceResult(
                    exception is IOException || exception is UnauthorizedAccessException
                        ? NovelPersistenceStatus.IOError : NovelPersistenceStatus.Corrupt,
                    exception.Message);
            }
        }

        private static string ComputeChecksum(string value)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
