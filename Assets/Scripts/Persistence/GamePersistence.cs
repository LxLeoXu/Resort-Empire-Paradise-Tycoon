using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Scene-scoped save/load coordinator. Saving is synchronous so pause cannot interrupt an awaited task.</summary>
[DefaultExecutionOrder(10000)]
public sealed class GamePersistence : MonoBehaviour
{
    public static GamePersistence Instance { get; private set; }
    public static bool IsRestoring { get; private set; }
    public static bool IsPaused { get; private set; }
#if UNITY_EDITOR
    public static string SaveDirectoryOverride;
#endif
    public string SavePath => store?.SavePath;
    public bool IsReady { get; private set; }
    public string LastError { get; private set; }
    public bool LoadedBackup => store != null && store.LoadedBackup;
    private WaveManager manager;
    private GameSaveStore store;
    private float resumeScale = 1f;
    private bool saving;
#if UNITY_EDITOR || SAVE_RECOVERY_TEST
    public bool RecoveryTestArmed { get; private set; }
    public bool CorruptPrimaryForRecoveryTest()
    {
        if (!IsReady || IsRestoring || RecoveryTestArmed) return false;
        // Two successful writes guarantee an up-to-date, valid backup, even on a new install.
        if (!SaveNow() || !SaveNow()) return false;
        SetApplicationPaused(true);
        RecoveryTestArmed = true;
        try
        {
            var expected = CaptureState();
            var details = new RecoveryTestExpectation { wave = expected.wave.number,
                coins = ResourceManager.Instance.GetResource(ResourceType.Coins) };
            System.IO.File.WriteAllText(SavePath + ".test-expected", JsonUtility.ToJson(details));
            using var file = new System.IO.FileStream(SavePath, System.IO.FileMode.Create, System.IO.FileAccess.Write);
            byte[] damaged = System.Text.Encoding.UTF8.GetBytes("{SCRUM-484 intentionally damaged primary save");
            file.Write(damaged, 0, damaged.Length);
            file.Flush(true);
            LastError = null;
            return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
    }
#endif
    private List<SaveExtension> retainedExtensions = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; IsRestoring = false; IsPaused = false; }

    public static void Ensure(WaveManager waveManager)
    {
        if (Instance != null) return;
        var coordinator = new GameObject("GamePersistence").AddComponent<GamePersistence>();
        coordinator.manager = waveManager;
#if UNITY_EDITOR || SAVE_RECOVERY_TEST
        SaveRecoveryTestPanel.EnsureExists();
#endif
    }
    private void Awake() { Instance = this; IsRestoring = true; }

    private IEnumerator Start()
    {
        // Let all scene Start handlers subscribe before restoring; spawned buildings also get a Start below.
        yield return null;
        if (manager == null || manager.Session == null) { IsRestoring = false; yield break; }
        string directory = Application.persistentDataPath;
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(SaveDirectoryOverride)) directory = SaveDirectoryOverride;
#endif
        store = new GameSaveStore(directory);
        if (store.TryLoad(out var state))
        {
            var restore = RestoreState(state);
            bool advancing = true;
            while (advancing)
            {
                object current = null;
                try { advancing = restore.MoveNext(); if (advancing) current = restore.Current; }
                catch (Exception error) { LastError = error.Message; advancing = false; }
                if (advancing) yield return current;
            }
            (restore as IDisposable)?.Dispose();
            if (!string.IsNullOrEmpty(LastError))
            {
                IsRestoring = false;
                Debug.LogWarning("Could not restore game: " + LastError + ". Automatic saving is disabled to preserve your save.", this);
                yield break;
            }
        }
        else
        {
            manager.StartFreshProgress();
            manager.Session.NotifyStateRestored();
            if (!string.IsNullOrEmpty(store.LastError)) Debug.LogWarning("Save could not be read; starting a new game. " + store.LastError, this);
        }
        IsRestoring = false;
        IsReady = true;
        manager.Session.WaveEnded += OnWaveEnded;
    }

    public GameState CaptureState()
    {
        if (!IsReady || IsRestoring) throw new InvalidOperationException("Game is not ready to save.");
        var data = new GameState { resources = ResourceManager.Instance.CaptureResources(), timeScale = IsPaused ? resumeScale : Time.timeScale };
        manager.CaptureProgress(data);
        var spots = FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var ids = new Dictionary<BuildingService, string>();
        foreach (var building in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        {
            var info = building.GetComponentInChildren<BuildingInfo>(true);
            if (info == null || !info.isBuilt) continue;
            var spot = spots.FirstOrDefault(s => s.SpawnedBuilding != null && s.SpawnedBuilding.GetComponentInChildren<BuildingService>() == building);
            string id = spot != null ? "spot:" + SceneId(spot.transform) : "building:" + SceneId(building.transform);
            ids.Add(building, id);
            var saved = building.CaptureState(id);
            if (spot != null) { saved.spotId = SceneId(spot.transform); saved.prefabIndex = spot.BuiltPrefabIndex; }
            data.buildings.Add(saved);
        }
        data.workers = WorkerRoster.Instance.CaptureState(ids);
        if (manager.Guests != null && manager.Guests.enabled) data.presentation = manager.Guests.CaptureState();
        FindFirstObjectByType<SatisfactionTracker>()?.CaptureState(data);
        var claim = FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
        if (claim != null) data.reward = claim.CaptureState();
        var extensions = new Dictionary<string, string>();
        foreach (var extension in retainedExtensions) extensions[extension.key] = extension.json;
        foreach (var participant in Participants())
        {
            if (string.IsNullOrEmpty(participant.SaveKey)) throw new InvalidOperationException("Save participant must have a stable key.");
            extensions[participant.SaveKey] = participant.CaptureSaveJson();
        }
        foreach (var extension in extensions) data.extensions.Add(new SaveExtension { key = extension.Key, json = extension.Value });
        return data;
    }

    public bool SaveNow()
    {
#if UNITY_EDITOR || SAVE_RECOVERY_TEST
        if (RecoveryTestArmed) return false;
#endif
        if (!IsReady || IsRestoring || saving) return false;
        saving = true;
        try
        {
            bool saved = store.Save(CaptureState());
            LastError = store.LastError;
            if (!saved) Debug.LogWarning("Game save failed; the previous save was kept. " + LastError, this);
            return saved;
        }
        catch (Exception error) { LastError = error.Message; Debug.LogWarning("Game save failed: " + LastError, this); return false; }
        finally { saving = false; }
    }

    public IEnumerator RestoreState(GameState data)
    {
        if (data == null || !data.IsValid()) { LastError = "Invalid save state."; yield break; }
        var spots = FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToDictionary(s => SceneId(s.transform));
        var buildings = FindObjectsByType<BuildingService>(FindObjectsSortMode.None).ToDictionary(b => "building:" + SceneId(b.transform));
        // Validate scene references before touching any live state.
        foreach (var b in data.buildings)
        {
            if (string.IsNullOrEmpty(b.spotId))
            { if (!buildings.ContainsKey(b.id)) { LastError = "Saved building no longer exists: " + b.id; yield break; } }
            else if (!spots.TryGetValue(b.spotId, out var spot) || b.prefabIndex < 0 || b.prefabIndex >= spot.buildingPrefabs.Count)
            { LastError = "Saved building spot/prefab no longer exists."; yield break; }
        }
        IsRestoring = true;
        float oldScale = Time.timeScale;
        bool restored = false;
        Time.timeScale = 0;
        try
        {
            foreach (var b in data.buildings)
            {
                if (!string.IsNullOrEmpty(b.spotId)) buildings[b.id] = spots[b.spotId].RestoreBuilding(b.prefabIndex).GetComponentInChildren<BuildingService>();
                var info = buildings[b.id].GetComponentInChildren<BuildingInfo>(true);
                info.currentLevel = b.level; info.isBuilt = true;
                buildings[b.id].GetComponentInChildren<BuildingLevels>(true)?.RefreshAfterLoad();
            }
            yield return null; // Starts bind freshly restored BuildingServices to the existing WaveSession.
            manager.RestoreProgress(data);
            foreach (var b in data.buildings) buildings[b.id].RestoreState(b);
            WorkerRoster.Instance.RestoreState(data.workers, buildings);
            ResourceManager.Instance.RestoreResources(data.resources);
            if (data.presentation != null) manager.Guests?.RestoreState(data.presentation);
            var satisfaction = FindFirstObjectByType<SatisfactionTracker>();
            satisfaction?.RestoreState(data);
            var claim = FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
            if (data.reward != null && claim != null) claim.RestoreState(data.reward);
            retainedExtensions = data.extensions ?? new List<SaveExtension>();
            foreach (var participant in Participants())
            {
                var extension = retainedExtensions.Find(e => e.key == participant.SaveKey);
                if (extension == null) continue;
                try { participant.RestoreSaveJson(extension.json); }
                catch (Exception error) { Debug.LogWarning("Could not restore " + participant.SaveKey + ": " + error.Message, this); }
            }
            manager.Session.NotifyStateRestored();
            if (manager.Session.State == WaveState.Result && claim != null && claim.HasPendingReward)
                FindFirstObjectByType<S4FigmaResultsUI>(FindObjectsInactive.Include)?.ShowWaveResults(manager.Session.WaveNumber,
                    manager.Session.ServedCount, manager.Session.EmittedCount, satisfaction != null ? satisfaction.CurrentPercent : 100,
                    restoreExistingReward: true);
            resumeScale = data.timeScale;
            LastError = null;
            restored = true;
        }
        finally
        {
            IsRestoring = false;
            Time.timeScale = IsPaused ? 0 : (restored ? resumeScale : oldScale);
        }
    }

    public void SetApplicationPaused(bool paused)
    {
#if UNITY_EDITOR || SAVE_RECOVERY_TEST
        if (RecoveryTestArmed) return;
#endif
        if (paused == IsPaused) return;
        if (paused)
        {
            resumeScale = Time.timeScale; IsPaused = true; Time.timeScale = 0;
            SaveNow();
        }
        else { IsPaused = false; Time.timeScale = resumeScale; }
    }
    private void OnApplicationPause(bool paused) => SetApplicationPaused(paused);
    private void OnApplicationQuit() { if (IsReady) SaveNow(); }
    private void OnWaveEnded(int wave) => StartCoroutine(SaveAfterResults());
    private IEnumerator SaveAfterResults() { yield return null; SaveNow(); }
    private void OnDestroy()
    {
        if (manager != null && manager.Session != null) manager.Session.WaveEnded -= OnWaveEnded;
        if (Instance == this) { Instance = null; IsRestoring = false; if (IsPaused) Time.timeScale = resumeScale; IsPaused = false; }
    }
    private static IEnumerable<IGameSaveParticipant> Participants() => FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,
        FindObjectsSortMode.None).OfType<IGameSaveParticipant>();
    public static string SceneId(Transform item)
    {
        string path = item.name;
        while (item.parent != null) { item = item.parent; path = item.name + "/" + path; }
        return item.gameObject.scene.name + "/" + path;
    }
}
