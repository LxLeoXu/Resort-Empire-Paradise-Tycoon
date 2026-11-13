using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Runs against the real scene, with a separate save directory under Temp.</summary>
[InitializeOnLoad]
public static class GameResumeVerification
{
    private const string Key = "GameResumeVerification.";
    private static double deadline;
    private static GameState expected;
    private static string pausedJson;
    private static int frame;

    static GameResumeVerification()
    {
        if (!SessionState.GetBool(Key + "active", false)) return;
        GamePersistence.SaveDirectoryOverride = SessionState.GetString(Key + "directory", "");
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Save Game/Verify pause and scene restart")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before running verification.");
        string directory = Path.GetFullPath(Path.Combine("Temp", "ResumeVerification", Guid.NewGuid().ToString("N")));
        SessionState.SetBool(Key + "active", true);
        SessionState.SetString(Key + "directory", directory);
        SessionState.SetString(Key + "stage", "prepare");
        SessionState.SetInt(Key + "previousInstance", 0);
        GamePersistence.SaveDirectoryOverride = directory;
        deadline = EditorApplication.timeSinceStartup + 120;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || GamePersistence.Instance == null || !GamePersistence.Instance.IsReady) return;
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Verification timed out");
            var persistence = GamePersistence.Instance;
            int previousInstance = SessionState.GetInt(Key + "previousInstance", 0);
            if (previousInstance != 0)
            {
                if (persistence.GetInstanceID() == previousInstance) return;
                SessionState.SetInt(Key + "previousInstance", 0);
            }
            var manager = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
            string stage = SessionState.GetString(Key + "stage", "");
            if (stage == "prepare")
            {
                Require(manager.Session.WaveNumber == 0, "No save starts at wave 1");
                var buildings = UnityEngine.Object.FindObjectsByType<BuildingService>(FindObjectsSortMode.None);
                var first = buildings[0];
                first.name = "RenamedBuildingForSaveTest";
                first.transform.SetParent(new GameObject("MovedBuildingForSaveTest").transform, true);
                first.GetComponentInChildren<BuildingInfo>(true).currentLevel = 2;
                first.GetComponentInChildren<BuildingLevels>(true)?.RefreshAfterLoad();
                WorkerRoster.Instance.Assign(WorkerRoster.Instance.Workers[0], first);
                ResourceManager.Instance.AddResource(ResourceType.Coins, 123);
                foreach (var spot in UnityEngine.Object.FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    spot.RestoreBuilding(0);
                typeof(WaveManager).GetField("arrivalDuration", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, 4f);
                manager.StartWave();
                Time.timeScale = 8;
                frame = Time.frameCount;
                SessionState.SetString(Key + "stage", "wait-guests");
            }
            else if (stage == "wait-guests")
            {
                if (Time.frameCount <= frame + 2 || manager.Session.PendingCount < 5 || manager.Session.QueuedCount < 1 || manager.Session.InServiceCount < 1) return;
                Time.timeScale = 1;
                foreach (var effect in UnityEngine.Object.FindObjectsByType<SpeedBoostEffect>(FindObjectsSortMode.None)) effect.Play(30);
                persistence.SetApplicationPaused(true);
                Require(persistence.LastError == null, "Pause saved: " + persistence.LastError);
                expected = persistence.CaptureState();
                Require(expected.IsValid(), "Captured running state is valid");
                pausedJson = JsonUtility.ToJson(expected);
                deadline = EditorApplication.timeSinceStartup + 3;
                frame = Time.frameCount;
                SessionState.SetString(Key + "stage", "paused");
            }
            else if (stage == "paused")
            {
                // Wall-clock time passes while scaled simulation time and queues stay unchanged.
                if (EditorApplication.timeSinceStartup < deadline - 1) return;
                Require(Time.frameCount > frame, "Editor frames advanced during pause");
                Require(JsonUtility.ToJson(persistence.CaptureState()) == pausedJson, "Wave, queues, NPCs, workers and boost stayed unchanged while paused");
                persistence.SetApplicationPaused(false);
                Require(Mathf.Approximately(Time.timeScale, 1), "Previous speed restored");
                Time.timeScale = 0;
                Require(persistence.SaveNow(), "Running wave saved");
                expected = persistence.CaptureState();
                File.WriteAllText(Path.Combine(GamePersistence.SaveDirectoryOverride, "expected.json"), JsonUtility.ToJson(expected));
                SessionState.SetString(Key + "stage", "reloading");
                deadline = EditorApplication.timeSinceStartup + 120;
                Reload(persistence);
            }
            else if (stage == "reloading")
            {
                expected ??= JsonUtility.FromJson<GameState>(File.ReadAllText(Path.Combine(GamePersistence.SaveDirectoryOverride, "expected.json")));
                var actual = persistence.CaptureState();
                Require(actual.wave.state == WaveState.Running && actual.wave.number == expected.wave.number, "Running wave and number restored");
                Require(actual.wave.pending.Count == expected.wave.pending.Count, "Pending count restored");
                for (int i = 0; i < actual.wave.pending.Count; i++)
                {
                    Require(Math.Abs(actual.wave.pending[i].arrival - expected.wave.pending[i].arrival) < 1e-9, "Arrival time round trip");
                    actual.wave.pending[i].arrival = expected.wave.pending[i].arrival;
                }
                Require(JsonUtility.ToJson(actual.wave) == JsonUtility.ToJson(expected.wave), "All pending needs, service IDs, arrivals, timer and counts restored");
                Require(JsonUtility.ToJson(actual.presentation) == JsonUtility.ToJson(expected.presentation), "Boat and NPC routes and positions restored");
                Require(actual.resources.Count == expected.resources.Count && actual.resources.All(r => expected.resources.Any(e => e.type == r.type && e.amount == r.amount)), "All currencies restored");
                Require(actual.buildings.Count == expected.buildings.Count, "Built buildings restored through BuildingSpot");
                foreach (var b in expected.buildings)
                {
                    var a = actual.buildings.Single(v => v.id == b.id);
                    Require(a.level == b.level && a.approaching.SequenceEqual(b.approaching), "Building level and incoming guests restored");
                    Require(JsonUtility.ToJson(new BuildingSave { seats = a.seats }) == JsonUtility.ToJson(new BuildingSave { seats = b.seats }), "Seat occupants and service progress restored");
                    Require(a.queue.Count == b.queue.Count, "Queue size restored");
                    for (int i = 0; i < a.queue.Count; i++) Require(a.queue[i].id == b.queue[i].id && Mathf.Abs(a.queue[i].waited - b.queue[i].waited) < .001f, "Queue order and elapsed wait restored");
                    if (b.boost != null) Require(JsonUtility.ToJson(a.boost) == JsonUtility.ToJson(b.boost), "Speed effect timer restored");
                }
                Require(actual.workers.Count == expected.workers.Count, "Worker inventory restored");
                for (int i = 0; i < actual.workers.Count; i++) Require(JsonUtility.ToJson(actual.workers[i]) == JsonUtility.ToJson(expected.workers[i]), "Worker assignment restored");
                Require(actual.satisfactionTotal == expected.satisfactionTotal && actual.satisfactionCount == expected.satisfactionCount, "Rating history restored");
                Debug.Log("RESUME VERIFICATION PASSED: active wave, background pause, queued waits, occupied seats, workers, currencies, buildings/levels, boat/NPC routes and speed effect. Artifacts: " + GamePersistence.SaveDirectoryOverride);
                Time.timeScale = 16;
                deadline = EditorApplication.timeSinceStartup + 120;
                SessionState.SetString(Key + "stage", "finish-wave");
            }
            else if (stage == "finish-wave")
            {
                var claim = UnityEngine.Object.FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
                Require(claim != null, "Scene has a ClaimRewards component");
                if (manager.Session.State != WaveState.Result || !claim.HasPendingReward) return;
                Time.timeScale = 0;
                Require(persistence.SaveNow(), "Pending S4 result saved");
                SessionState.SetString(Key + "stage", "claim-restored-result");
                Reload(persistence);
            }
            else if (stage == "claim-restored-result")
            {
                var claim = UnityEngine.Object.FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
                Require(manager.Session.State == WaveState.Result && claim != null && claim.HasPendingReward, "Unclaimed reward restored");
                int before = ResourceManager.Instance.GetResource(ResourceType.Coins);
                int reward = claim.CaptureState().rewards.coins;
                claim.Claim();
                int after = ResourceManager.Instance.GetResource(ResourceType.Coins);
                Require(after == before + reward, "Claim credited reward once");
                var store = new GameSaveStore(GamePersistence.SaveDirectoryOverride);
                Require(store.TryLoad(out var saved) && saved.reward.claimed && saved.resources.Single(r => r.type == ResourceType.Coins).amount == after, "Claim automatically saved AFTER reward credit");
                SessionState.SetString(Key + "stage", "claimed-restart");
                Reload(persistence);
            }
            else if (stage == "claimed-restart")
            {
                var claim = UnityEngine.Object.FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
                Require(claim != null && !claim.HasPendingReward && claim.CaptureState().claimed, "Claimed result restored");
                int before = ResourceManager.Instance.GetResource(ResourceType.Coins);
                claim.Claim();
                Require(ResourceManager.Instance.GetResource(ResourceType.Coins) == before, "Restart cannot duplicate a claimed reward");
                Debug.Log("CLAIM VERIFICATION PASSED: completed wave, pending result restore, claim autosave after credit, restart without duplicate reward.");
                Require(persistence.SaveNow(), "Create valid backup for scene recovery test");
                Require(persistence.CorruptPrimaryForRecoveryTest(), "Device test action damaged the primary");
                string damaged = File.ReadAllText(persistence.SavePath);
                Require(!persistence.SaveNow(), "Autosave blocked after intentional damage");
                persistence.SetApplicationPaused(false);
                Require(GamePersistence.IsPaused && Time.timeScale == 0, "Test stays paused after returning from background");
                Require(File.ReadAllText(persistence.SavePath) == damaged, "Autosave did not repair the damaged primary");
                SessionState.SetInt(Key + "coins", before);
                SessionState.SetString(Key + "stage", "backup-restart");
                Reload(persistence);
            }
            else if (stage == "backup-restart")
            {
                Require(persistence.LoadedBackup, "Real scene loaded backup after primary was manually corrupted");
                Require(ResourceManager.Instance.GetResource(ResourceType.Coins) == SessionState.GetInt(Key + "coins", -1), "Backup restored currencies");
                Debug.Log("SCENE BACKUP VERIFICATION PASSED: manually corrupted primary, scene reopened, backup loaded without a crash.");
                UnityEngine.Object.FindFirstObjectByType<SaveRecoveryTestPanel>().Open();
                ScreenCapture.CaptureScreenshot(Path.Combine(GamePersistence.SaveDirectoryOverride, "recovery-panel.png"));
                frame = Time.frameCount;
                SessionState.SetString(Key + "stage", "panel-capture");
            }
            else if (stage == "panel-capture")
            {
                if (Time.frameCount < frame + 4) return;
                expected = persistence.CaptureState();
                var legacy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(expected));
                legacy.version = 1;
                foreach (var b in legacy.buildings)
                {
                    string original = b.id;
                    b.id = "building:OldScene/RemovedParent/" + b.id;
                    foreach (var w in legacy.workers) if (w.buildingId == original) w.buildingId = b.id;
                }
                Require(new GameSaveStore(GamePersistence.SaveDirectoryOverride).Save(legacy), "Legacy path-based save created");
                SessionState.SetString(Key + "stage", "legacy-restart");
                Reload(persistence);
            }
            else if (stage == "legacy-restart")
            {
                var actual = persistence.CaptureState();
                Require(actual.version == 2 && actual.buildings.All(b => !b.id.Contains("/")), "Stable keys replace hierarchy paths");
                Require(actual.buildings.Count == expected.buildings.Count && actual.workers.All(w => string.IsNullOrEmpty(w.buildingId) || actual.buildings.Any(b => b.id == w.buildingId)), "Legacy buildings and worker assignments migrated");
                Require(persistence.SaveNow(), "Migrated save can be written");
                var missingPrefab = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(actual));
                string previousId = missingPrefab.buildings[0].id;
                missingPrefab.buildings[0].id = "removed-building";
                foreach (var w in missingPrefab.workers) if (w.buildingId == previousId) w.buildingId = "removed-building";
                Require(new GameSaveStore(GamePersistence.SaveDirectoryOverride).Save(missingPrefab), "Primary with missing prefab prepared");
                SessionState.SetString(Key + "stage", "semantic-backup");
                Reload(persistence);
            }
            else if (stage == "semantic-backup")
            {
                Require(persistence.LoadedBackup && persistence.CaptureState().buildings.All(b => b.id != "removed-building"), "Unavailable primary building falls back to compatible backup");
                Require(persistence.SaveNow(), "Saving continues after semantic backup recovery");
                manager.Guests?.RestoreState(new GuestPresentationSave { docked = true });
                manager.StartWave();
                Time.timeScale = 8;
                SessionState.SetString(Key + "stage", "incompatible-wave");
            }
            else if (stage == "incompatible-wave")
            {
                var actual = persistence.CaptureState();
                var occupied = actual.buildings.FirstOrDefault(b => b.seats.Count > 0);
                if (occupied == null) return;
                Time.timeScale = 0;
                expected = persistence.CaptureState();
                var incompatible = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(expected));
                incompatible.buildings.First(b => b.seats.Count > 0).seats[0].index = 999;
                Require(new GameSaveStore(GamePersistence.SaveDirectoryOverride).Save(incompatible), "Save with seat unavailable in current prefab created");
                SessionState.SetString(Key + "stage", "wave-restarted");
                Reload(persistence);
            }
            else if (stage == "wave-restarted")
            {
                var actual = persistence.CaptureState();
                Require(actual.wave.number == expected.wave.number && actual.wave.state == WaveState.Running && actual.wave.elapsed == 0 && actual.wave.emitted == 0 && actual.wave.served == 0, "Same interrupted wave restarts from zero");
                Require(actual.resources.All(r => expected.resources.Any(e => e.type == r.type && e.amount == r.amount)), "Wave restart preserves currencies");
                Require(actual.buildings.All(b => expected.buildings.Any(e => e.id == b.id && e.level == b.level)), "Wave restart preserves buildings and levels");
                Require(actual.workers.Select(w => w.buildingId).SequenceEqual(expected.workers.Select(w => w.buildingId)), "Wave restart preserves worker assignments");
                Require(!string.IsNullOrEmpty(persistence.RecoveryMessage) && persistence.SaveNow(), "Recovery is reported and autosaving continues");
                Debug.Log("COMPATIBILITY VERIFICATION PASSED: renamed/reparented building, stable keys, legacy migration, incompatible seat restarts same wave preserving progress and saving.");
                var store = new GameSaveStore(GamePersistence.SaveDirectoryOverride);
                File.WriteAllText(store.SavePath, "{broken primary");
                File.WriteAllText(store.BackupPath, "{broken backup");
                SessionState.SetString(Key + "stage", "fresh-after-corruption");
                Reload(persistence);
            }
            else if (stage == "fresh-after-corruption")
            {
                var actual = persistence.CaptureState();
                var store = new GameSaveStore(GamePersistence.SaveDirectoryOverride);
                Require(actual.wave.number == 0 && actual.wave.state == WaveState.Waiting, "Both corrupt files start at wave 1");
                Require(actual.resources.All(r => ResourceManager.Instance.CaptureStartingResources().Any(e => e.type == r.type && e.amount == r.amount)), "Fresh game restores starting currencies");
                Require(File.Exists(store.BrokenPath) && !string.IsNullOrEmpty(persistence.RecoveryMessage), "Broken primary archived and player informed");
                Require(persistence.SaveNow() && store.TryLoad(out _), "Fresh game saves normally after corruption");
                Debug.Log("FRESH RECOVERY VERIFICATION PASSED: both corrupt files archived, fresh game initialized, player notified, saving remains enabled.");
                var incompatibleProgress = persistence.CaptureState();
                incompatibleProgress.buildings[0].id = "removed-building";
                Require(store.Save(incompatibleProgress) && store.Save(incompatibleProgress), "Both save versions reference an unavailable prefab");
                SessionState.SetString(Key + "stage", "fresh-after-incompatible-progress");
                Reload(persistence);
            }
            else if (stage == "fresh-after-incompatible-progress")
            {
                Require(persistence.IsReady && manager.Session.WaveNumber == 0 && !string.IsNullOrEmpty(persistence.RecoveryMessage), "Both incompatible scene saves start a new game without disabling persistence");
                Require(persistence.SaveNow(), "Saving continues after failed primary AND backup scene restoration");
                Debug.Log("SEMANTIC RECOVERY VERIFICATION PASSED: unavailable prefab in both saves archived, new game restored, autosave remains enabled.");
                Require(UnityEngine.Object.FindFirstObjectByType<SaveRecoveryNotice>() != null, "Player sees a recovery notice");
                ScreenCapture.CaptureScreenshot(Path.Combine(GamePersistence.SaveDirectoryOverride, "recovery-notice.png"));
                frame = Time.frameCount;
                SessionState.SetString(Key + "stage", "notice-capture");
            }
            else if (stage == "notice-capture")
            {
                if (Time.frameCount < frame + 4) return;
                Finish();
            }
        }
        catch (Exception error) { Debug.LogException(error); Finish(); }
    }

    private static void Reload(GamePersistence persistence)
    {
        SessionState.SetInt(Key + "previousInstance", persistence.GetInstanceID());
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static void Finish()
    {
        SessionState.SetBool(Key + "active", false);
        EditorApplication.update -= Tick;
        // Keep override until Play mode exits so OnApplicationQuit also writes only the test directory.
        EditorApplication.isPlaying = false;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Resume regression failed: " + message);
    }
}
