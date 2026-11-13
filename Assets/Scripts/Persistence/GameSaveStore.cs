using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>One synchronous, durable write followed by atomic replacement on the same filesystem.</summary>
public sealed class GameSaveStore
{
    [Serializable] private sealed class Envelope { public string json, checksum; }
    public string SavePath { get; }
    public string BackupPath => SavePath + ".bak";
    public string TemporaryPath => SavePath + ".tmp";
    public string BrokenPath => Path.Combine(Path.GetDirectoryName(SavePath), "game-save.broken.json");
    public string LastError { get; private set; }
    public bool LoadedBackup { get; private set; }

    public GameSaveStore(string directory) { SavePath = Path.Combine(directory, "game-save.json"); }

    public bool Save(GameState state)
    {
        try
        {
            if (state == null || !state.IsValid()) throw new InvalidDataException("Invalid game state; existing save was kept.");
            string json = JsonUtility.ToJson(state);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { json = json, checksum = Hash(json) }));
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            using (var stream = new FileStream(TemporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(SavePath))
            {
                // A corrupt primary must never replace the last valid backup.
                bool validPrimary = TryRead(SavePath, out _);
                try { File.Replace(TemporaryPath, SavePath, validPrimary ? BackupPath : null); }
                catch (PlatformNotSupportedException)
                {
                    if (validPrimary)
                    {
                        File.Copy(SavePath, BackupPath, true);
                        using var backup = new FileStream(BackupPath, FileMode.Open, FileAccess.Write, FileShare.None);
                        backup.Flush(true);
                    }
                    // Older Unity profiles have no overwrite overload. The durable backup
                    // remains authoritative if the process stops between delete and rename.
                    File.Delete(SavePath);
                    File.Move(TemporaryPath, SavePath);
                }
            }
            else File.Move(TemporaryPath, SavePath);
            LastError = null;
            return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
    }

    public bool TryLoad(out GameState state)
    {
        LoadedBackup = false;
        if (TryRead(SavePath, out state)) { LastError = null; return true; }
        if (TryRead(BackupPath, out state)) { LoadedBackup = true; LastError = null; return true; }
        return false;
    }

    public bool TryLoadBackup(out GameState state)
    {
        if (!TryRead(BackupPath, out state)) return false;
        LoadedBackup = true;
        LastError = null;
        return true;
    }

    // Keep forensic copies; never overwrite a previously archived save.
    public bool ArchiveBrokenSaves()
    {
        try
        {
            Archive(SavePath, BrokenPath);
            Archive(BackupPath, Path.Combine(Path.GetDirectoryName(SavePath), "game-save.backup.broken.json"));
            return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
    }

    private static void Archive(string source, string destination)
    {
        if (!File.Exists(source)) return;
        if (File.Exists(destination)) destination += "." + Guid.NewGuid().ToString("N");
        File.Move(source, destination);
    }

    private bool TryRead(string path, out GameState state)
    {
        state = null;
        try
        {
            if (!File.Exists(path)) return false;
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Save file is too large.");
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
            if (envelope == null || string.IsNullOrEmpty(envelope.json) || envelope.checksum != Hash(envelope.json))
                throw new InvalidDataException("Save checksum does not match.");
            var candidate = JsonUtility.FromJson<GameState>(envelope.json);
            if (candidate == null || !candidate.IsProgressValid()) throw new InvalidDataException("Invalid or unsupported save progress.");
            state = candidate;
            return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
    }

    private static string Hash(string json)
    {
        using var sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
    }
}
