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
    public string RecoveryMessage { get; private set; }
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
        var initial = CaptureWorldState();
        initial.resources = ResourceManager.Instance.CaptureStartingResources();
        initial.wave = new WaveSave();
        initial.relaxDebuted = initial.oceanViewDebuted = false; initial.lastExecutedWave = null;
        if (store.TryLoad(out var state))
        {
            yield return TryRestore(state);
            if (!string.IsNullOrEmpty(LastError) && !store.LoadedBackup && store.TryLoadBackup(out var backup))
                yield return TryRestore(backup);
            if (!string.IsNullOrEmpty(LastError))
            {
                SetRecoveryMessage("Uložený postup sa nepodarilo obnoviť. Začína nová hra; pôvodné uloženie bolo odložené.");
                ArchiveBrokenSaves();
                foreach (var spot in FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (spot.SpawnedBuilding != null) spot.ResetRestoredBuilding();
                yield return TryRestore(initial);
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(store.LastError))
            {
                SetRecoveryMessage("Uloženie ani záloha sa nedajú načítať. Začína nová hra; poškodené súbory boli odložené.");
                ArchiveBrokenSaves();
            }
            manager.StartFreshProgress();
            ResourceManager.Instance.RestoreResources(initial.resources);
            manager.Session.NotifyStateRestored();
        }
        IsRestoring = false;
        IsReady = true;
        manager.Session.WaveEnded += OnWaveEnded;
        if (!string.IsNullOrEmpty(RecoveryMessage)) SaveRecoveryNotice.Show(RecoveryMessage);
    }

    private IEnumerator TryRestore(GameState data)
    {
        LastError = null;
        var restore = RestoreState(data);
        bool advancing = true;
        while (advancing)
        {
            object current = null;
            try { advancing = restore.MoveNext(); if (advancing) current = restore.Current; }
            catch (Exception error) { LastError = error.Message; advancing = false; }
            if (advancing) yield return current;
        }
        (restore as IDisposable)?.Dispose();
    }

    private void ArchiveBrokenSaves()
    {
        if (!store.ArchiveBrokenSaves()) Debug.LogWarning("Could not archive save: " + store.LastError, this);
    }
    private void SetRecoveryMessage(string message) { RecoveryMessage = message; Debug.LogWarning(message, this); }

    public GameState CaptureState()
    {
        if (!IsReady || IsRestoring) throw new InvalidOperationException("Game is not ready to save.");
        return CaptureWorldState();
    }

    private GameState CaptureWorldState()
    {
        var data = new GameState { resources = ResourceManager.Instance.CaptureResources(), timeScale = IsPaused ? resumeScale : Time.timeScale };
        manager.CaptureProgress(data);
        var ids = new Dictionary<BuildingService, string>();
        foreach (var building in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        {
            var info = building.GetComponentInChildren<BuildingInfo>(true);
            if (info == null || !info.isBuilt) continue;
            string id = info.SaveKey;
            if (string.IsNullOrWhiteSpace(id) || ids.ContainsValue(id)) throw new InvalidOperationException("Missing or duplicate building SaveKey: " + id);
            ids.Add(building, id);
            var saved = building.CaptureState(id);
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
        if (data == null || !data.IsProgressValid()) { LastError = "Invalid save progress."; yield break; }
        // Clone before migrating v1 so callers and the original archived data remain unchanged.
        data = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(data));
        var spots = FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var buildings = FindObjectsByType<BuildingService>(FindObjectsSortMode.None)
            .ToDictionary(b => b.GetComponentInChildren<BuildingInfo>(true).SaveKey);
        var spawn = new Dictionary<string, (BuildingSpot spot, int index)>();
        foreach (var spot in spots)
            for (int i = 0; i < spot.buildingPrefabs.Count; i++)
            {
                var info = spot.buildingPrefabs[i] != null ? spot.buildingPrefabs[i].GetComponentInChildren<BuildingInfo>(true) : null;
                if (info != null && !string.IsNullOrEmpty(info.SaveKey) && !spawn.ContainsKey(info.SaveKey)) spawn.Add(info.SaveKey, (spot, i));
            }
        if (data.version == 1)
        {
            var allInfos = buildings.Values.Select(b => b.GetComponentInChildren<BuildingInfo>(true))
                .Concat(spawn.Values.Select(s => s.spot.buildingPrefabs[s.index].GetComponentInChildren<BuildingInfo>(true)));
            var keysByNeed = allInfos.GroupBy(i => i.offeredNeed).ToDictionary(g => g.Key, g => g.First().SaveKey);
            var migrated = new Dictionary<string, string>();
            foreach (var saved in data.buildings)
            {
                if (!keysByNeed.TryGetValue(saved.need, out var key)) { LastError = "Cannot migrate saved building: " + saved.id; yield break; }
                migrated.Add(saved.id, key); saved.id = key;
            }
            foreach (var w in data.workers) if (!string.IsNullOrEmpty(w.buildingId)) w.buildingId = migrated[w.buildingId];
            data.version = 2;
        }
        // Permanent identities, not object names, hierarchy paths, spot paths or prefab list order.
        var selectedSpots = new HashSet<BuildingSpot>();
        foreach (var b in data.buildings)
            if (!buildings.ContainsKey(b.id))
            {
                if (!spawn.TryGetValue(b.id, out var target) || target.spot.SpawnedBuilding != null || !selectedSpots.Add(target.spot))
                { LastError = "Saved building prefab is unavailable: " + b.id; yield break; }
            }
        IsRestoring = true;
        float oldScale = Time.timeScale;
        bool restored = false;
        Time.timeScale = 0;
        try
        {
            foreach (var b in data.buildings)
            {
                if (!buildings.ContainsKey(b.id))
                {
                    var target = spawn[b.id];
                    buildings[b.id] = target.spot.RestoreBuilding(target.index).GetComponentInChildren<BuildingService>();
                }
                var info = buildings[b.id].GetComponentInChildren<BuildingInfo>(true);
                info.currentLevel = Mathf.Clamp(b.level, 1, BuildingInfo.MaxLevel); info.isBuilt = true;
                buildings[b.id].GetComponentInChildren<BuildingLevels>(true)?.RefreshAfterLoad();
            }
            yield return null; // Starts bind freshly restored BuildingServices to the existing WaveSession.
            WorkerRoster.Instance.RestoreState(data.workers, buildings);
            ResourceManager.Instance.RestoreResources(data.resources);
            var satisfaction = FindFirstObjectByType<SatisfactionTracker>();
            var claim = FindFirstObjectByType<ClaimRewards>(FindObjectsInactive.Include);
            bool resumed = false;
            string waveError = null;
            try
            {
                if (!data.IsValid()) throw new InvalidOperationException("Incompatible saved wave data.");
                if (data.wave.state == WaveState.Running && manager.Guests != null && manager.Guests.enabled && data.presentation == null)
                    throw new InvalidOperationException("Saved guest presentation is missing.");
                manager.RestoreProgress(data);
                foreach (var b in data.buildings) buildings[b.id].RestoreState(b);
                if (data.presentation != null) manager.Guests?.RestoreState(data.presentation);
                satisfaction?.RestoreState(data);
                if (data.reward != null && claim != null) claim.RestoreState(data.reward);
                resumed = true;
            }
            catch (Exception error) { waveError = error.Message; }
            if (!resumed)
            {
                manager.RestoreProgress(data, restoreWave: false);
                manager.RestoreIdleProgress(data.wave);
                foreach (var building in buildings.Values)
                {
                    building.RestoreState(new BuildingSave());
                    building.GetComponent<SpeedBoostEffect>()?.Stop();
                }
                manager.Guests?.RestoreState(new GuestPresentationSave { docked = true });
                satisfaction?.RestoreState(new GameState());
                if (claim != null) claim.RestoreState(data.wave.state == WaveState.Running ? new RewardSave { waveId = data.wave.number - 1 } :
                    (data.reward != null && data.reward.rewards != null && data.reward.rewards.coins >= 0 && data.reward.rewards.energy >= 0 && data.reward.rewards.resortValue >= 0 ? data.reward : new RewardSave()));
                if (data.wave.state == WaveState.Running)
                {
                    manager.RestartSavedWave(data.wave);
                    SetRecoveryMessage("Rozbehnutú vlnu sa nepodarilo obnoviť. Tá istá vlna začína odznova; meny, budovy a pracovníci zostali zachovaní.");
                }
                else SetRecoveryMessage("Postup bol obnovený; nekompatibilné údaje hostí boli vyčistené.");
                Debug.LogWarning("Wave recovery: " + waveError, this);
            }
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
            resumeScale = !float.IsNaN(data.timeScale) && !float.IsInfinity(data.timeScale) && data.timeScale >= 0 ? data.timeScale : 1f;
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
}
