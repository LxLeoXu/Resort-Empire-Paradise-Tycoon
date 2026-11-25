using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Isolated Edit Mode regression checks. Never reads/writes the player's save directory.
public static class MaterialsPersistenceTests
{
    private static int passed;
    [Serializable] private sealed class Envelope { public string json, checksum; }

    [MenuItem("Tools/Tests/SCRUM-519 Materials Persistence")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || ResourceManager.Instance != null || GamePersistence.Instance != null)
            throw new InvalidOperationException("Run in Edit Mode without a live game session.");
        passed = 0;
        string directory = Path.GetFullPath(Path.Combine("Temp", "SCRUM-519", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        var store = new GameSaveStore(directory);
        var owner = new GameObject("SCRUM-519 isolated test");
        var instanceField = typeof(ResourceManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var catalog = ScriptableObject.CreateInstance<WaveCatalog>();
        try
        {
            var manager = owner.AddComponent<ResourceManager>();
            instanceField.SetValue(null, manager);
            manager.RestoreResources(manager.CaptureStartingResources());
            int events = 0;
            manager.OnResourceChanged += (type, amount) => { if (type == ResourceType.Materials) events++; };
            var state = new GameState { resources = manager.CaptureResources(), wave = new WaveSave { number = 1, state = WaveState.Result } };
            int initial = manager.GetResource(ResourceType.Materials);
            Check("Materials numeric ID remains former Wood ID", (int)ResourceType.Materials == 3);
            Check("Resource enum serialized numerically", JsonUtility.ToJson(new ResourceSave { type = ResourceType.Materials, amount = 73 }).Contains("\"type\":3"));
            Check("Initial state valid", state.IsValid());
            Check("Save initial resources", store.Save(state));
            Check("Read initial resources", store.TryLoad(out var loaded));
            Check("All resources preserved", JsonUtility.ToJson(state) == JsonUtility.ToJson(loaded));

            var claim = owner.AddComponent<ClaimRewards>();
            Check("Prepare reward", claim.SetRewardsForWave(1, new WaveRewardsData(380, 0, 48, 7)));
            state.reward = claim.CaptureState();
            Check("Save pending reward", store.Save(state));
            Check("Load pending reward", store.TryLoad(out loaded));
            UnityEngine.Object.DestroyImmediate(claim);
            claim = owner.AddComponent<ClaimRewards>();
            claim.RestoreState(loaded.reward);
            Check("Pending Materials retained", claim.HasPendingReward && claim.CaptureState().rewards.materials == 7);
            Check("Preparing/restoring does not grant Materials", manager.GetResource(ResourceType.Materials) == initial);
            claim.Claim();
            Check("Claim grants Materials and emits event", manager.GetResource(ResourceType.Materials) == initial + 7 && events == 1);
            claim.Claim();
            Check("Second claim rejected", manager.GetResource(ResourceType.Materials) == initial + 7 && events == 1);
            state.resources = manager.CaptureResources(); state.reward = claim.CaptureState();
            Check("Save claimed state", store.Save(state));
            Check("Load claimed state", store.TryLoad(out loaded));
            manager.RestoreResources(loaded.resources);
            UnityEngine.Object.DestroyImmediate(claim); claim = owner.AddComponent<ClaimRewards>();
            claim.RestoreState(loaded.reward); claim.Claim();
            Check("Reload does not grant same reward again", manager.GetResource(ResourceType.Materials) == initial + 7 && !claim.HasPendingReward);
            Check("Same wave cannot be prepared after reload", !claim.SetRewardsForWave(1, new WaveRewardsData(380, 0, 48, 7)));
            Check("Coins, Energy, Reputation, Resort Value preserved", loaded.resources.All(r => manager.GetResource(r.type) == r.amount));

            state.reward.rewards.materials = -1;
            Check("Negative reward rejected", !state.IsValid() && !store.Save(state));
            bool rejected = false;
            try { claim.RestoreState(state.reward); } catch (InvalidOperationException) { rejected = true; }
            Check("Reward restore rejects negative Materials", rejected);
            state.reward.rewards.materials = 7;
            var materials = state.resources.Find(r => r.type == ResourceType.Materials);
            materials.amount = -1;
            Check("Negative balance rejected", !state.IsProgressValid() && !store.Save(state));
            materials.amount = 73;
            Check("Failed writes preserve previous save", store.TryLoad(out loaded) && loaded.resources.Find(r => r.type == ResourceType.Materials).amount == initial + 7);

            // Fixtures use the real envelope/checksum and Unity JsonUtility, including missing fields.
            state.reward = null;
            foreach (int version in new[] { 1, 2 })
            {
                state.version = version;
                string woodJson = JsonUtility.ToJson(state);
                WriteFixture(store.SavePath, woodJson);
                Check("Legacy numeric Wood value preserved v" + version, store.TryLoad(out loaded) && loaded.resources.Find(r => r.type == ResourceType.Materials).amount == 73);
                state.resources.Remove(materials);
                string legacyJson = JsonUtility.ToJson(state);
                WriteFixture(store.SavePath, legacyJson);
                string original = File.ReadAllText(store.SavePath);
                Check("Missing Materials migrated v" + version, store.TryLoad(out loaded) && loaded.IsValid() && loaded.resources.Find(r => r.type == ResourceType.Materials).amount == 0);
                Check("Loading does not rewrite legacy file v" + version, File.ReadAllText(store.SavePath) == original);
                loaded.MigrateLegacyResources();
                Check("Migration idempotent v" + version, loaded.resources.Count == 7);
                state.resources.Add(materials);
            }
            state.resources.RemoveAll(r => r.type == ResourceType.Coins);
            state.MigrateLegacyResources();
            Check("Missing other currency not silently repaired", !state.IsProgressValid());
            var oldReward = JsonUtility.FromJson<WaveRewardsData>("{\"coins\":10,\"energy\":0,\"resortValue\":1}");
            Check("Legacy reward without Materials defaults to zero", oldReward.materials == 0);

            var wave = new WaveSession(catalog) { VipEveryNthGuest = 2 };
            wave.StartWave(10, null, new WaveDefinition(6, 6, 0, 0, 0, 0));
            wave.Tick(6);
            foreach (var need in wave.PendingNeeds.OrderBy(n => n.Id).Take(2).ToArray()) { wave.BeginService(need.Id); wave.ResolveNeed(need.Id); }
            Check("Fixture served one VIP", wave.VipServedCount == 1);
            var savedWave = JsonUtility.FromJson<WaveSave>(JsonUtility.ToJson(wave.CaptureState()));
            var restoredWave = new WaveSession(catalog);
            restoredWave.RestoreState(savedWave);
            Check("VIP count and cadence restored", restoredWave.VipServedCount == 1 && restoredWave.VipEveryNthGuest == 2);
            Check("Pending VIP flags restored", restoredWave.PendingNeeds.Count(n => n.IsVip) == 1);
            restoredWave.Tick(4); // Future arrivals must retain the saved VIP cadence.
            foreach (var need in restoredWave.PendingNeeds.ToArray()) { restoredWave.BeginService(need.Id); restoredWave.ResolveNeed(need.Id); }
            Check("Resumed wave counts VIP once", restoredWave.VipServedCount == 3 && restoredWave.State == WaveState.Result);
            restoredWave.RestoreState(new WaveSave());
            Check("Fresh state clears VIP count and restores legacy cadence", restoredWave.VipServedCount == 0 && restoredWave.VipEveryNthGuest == 5);
            var invalid = new GameState { resources = manager.CaptureResources(), wave = new WaveSave { vipServed = -1 } };
            Check("Negative VIP count rejected", !invalid.IsValid());
            invalid.wave.vipServed = 1;
            Check("VIP count above served rejected", !invalid.IsValid());
            Debug.Log("SCRUM-519 PASS: " + passed + " checks. Isolated saves: " + directory);
        }
        finally
        {
            instanceField.SetValue(null, null);
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(catalog);
        }
    }

    private static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("SCRUM-519 FAIL: " + name);
        passed++;
        Debug.Log("SCRUM-519 PASS: " + name);
    }
    private static void WriteFixture(string path, string json)
    {
        using var sha = SHA256.Create();
        string checksum = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(json)));
        File.WriteAllText(path, JsonUtility.ToJson(new Envelope { json = json, checksum = checksum }));
    }
}
