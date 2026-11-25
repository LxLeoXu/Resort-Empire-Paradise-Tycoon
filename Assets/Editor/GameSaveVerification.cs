using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated regression checks. Never reads or writes the player's real save.</summary>
public static class GameSaveVerification
{
    [MenuItem("Tools/Save Game/Verify file recovery")]
    public static void VerifyStore()
    {
        string directory = Path.GetFullPath(Path.Combine("Temp", "SaveVerification", Guid.NewGuid().ToString("N")));
        var store = new GameSaveStore(directory);
        Require(!store.TryLoad(out _), "No save starts a fresh game");
        var state = NewState();
        Require(store.Save(state), "Initial save: " + store.LastError);
        Require(store.TryLoad(out var loaded) && loaded.resources[0].amount == 10, "JSON round trip");
        state.resources[0].amount = 20;
        Require(store.Save(state), "Second save");
        Require(File.Exists(store.BackupPath), "Previous version retained");
        File.WriteAllText(store.TemporaryPath, "interrupted new save");
        Require(store.TryLoad(out loaded) && loaded.resources[0].amount == 20, "Incomplete temporary file ignored");
        File.WriteAllText(store.SavePath, "{broken JSON");
        Require(store.TryLoad(out loaded) && store.LoadedBackup && loaded.resources[0].amount == 10, "Corrupt primary falls back to backup");
        state.resources[0].amount = 30;
        Require(store.Save(state), "Save after recovery");
        File.WriteAllText(store.SavePath, "corrupt again");
        Require(store.TryLoad(out loaded) && loaded.resources[0].amount == 10, "Recovery never backs up a corrupt primary");
        state.resources[0].amount = -1;
        Require(!store.Save(state), "Invalid state rejected");
        Require(store.TryLoad(out loaded) && loaded.resources[0].amount == 10, "Rejected save preserves backup");
        File.WriteAllText(store.BackupPath, "also corrupt");
        Require(!store.TryLoad(out _), "Both corrupt files fail gracefully");
        Require(store.ArchiveBrokenSaves() && File.Exists(store.BrokenPath) && !File.Exists(store.SavePath) && !File.Exists(store.BackupPath), "Broken saves archived");
        state.resources[0].amount = 40;
        Require(store.Save(state) && store.TryLoad(out loaded) && loaded.resources[0].amount == 40, "Saving continues after both files are corrupt");
        File.WriteAllText(store.SavePath, "broken a second time");
        Require(store.ArchiveBrokenSaves() && File.ReadAllText(store.BrokenPath) == "corrupt again", "An earlier broken save is never overwritten");
        var maxLevel = NewState();
        maxLevel.buildings.Add(new BuildingSave { id = "reception", need = NeedType.Rooms, level = BuildingInfo.MaxLevel });
        Require(maxLevel.IsValid(), "BuildingInfo.MaxLevel accepted");
        maxLevel.buildings[0].level++;
        Require(!maxLevel.IsProgressValid(), "Levels above BuildingInfo.MaxLevel rejected");
        state.wave.elapsed = -1; // Simulate a previous writer with incompatible transient state and intact progress.
        string inner = JsonUtility.ToJson(state);
        using (var hash = System.Security.Cryptography.SHA256.Create())
        {
            string checksum = Convert.ToBase64String(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(inner)));
            File.WriteAllText(store.SavePath, JsonUtility.ToJson(new TestEnvelope { json = inner, checksum = checksum }));
        }
        Require(store.TryLoad(out loaded) && loaded.IsProgressValid() && !loaded.IsValid(), "Invalid transient wave still exposes recoverable progress");
        Debug.Log("SAVE VERIFICATION PASSED: round trip, previous backup, interrupted write, corrupt primary, repeated recovery, invalid state, both corrupt. Artifacts: " + directory);
    }

    private static GameState NewState()
    {
        var state = new GameState { wave = new WaveSave() };
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            state.resources.Add(new ResourceSave { type = type, amount = 10 });
        return state;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Save regression failed: " + message);
    }
    [Serializable] private sealed class TestEnvelope { public string json, checksum; }
}
