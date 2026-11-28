using System;
using UnityEngine;

/// <summary>
/// SCRUM-22: in wave 2, around second 45, a sudden rush hits a building with no worker. Shows the "Sudden rush at ...!"
/// popup (Move Worker here / Speed Up / Ignore). The wave keeps running. Move Worker here costs no energy (SCRUM-88).
/// </summary>
[DisallowMultipleComponent]
public sealed class SuddenRushController : MonoBehaviour, IGameSaveParticipant
{
    [Serializable] private sealed class SaveData { public int wave; public bool done; }

    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private SuddenRushPanel panel;
    [SerializeField] private WorkerAssignUI workerAssign;

    [Header("Nával")]
    [Min(1)] [SerializeField] private int rushWave = 2;
    [Tooltip("Sekunda vlny, v ktorej nával príde.")]
    [Min(1f)] [SerializeField] private float rushAtSeconds = 45f;
    [Tooltip("Koľko hostí navyše príde.")]
    [Min(1)] [SerializeField] private int rushGuests = 4;

    private WaveSession session;
    private BuildingService target;
    private bool rushDone;

    public string SaveKey => "sudden-rush";

    public string CaptureSaveJson() => JsonUtility.ToJson(new SaveData { wave = rushWave, done = rushDone });

    public void RestoreSaveJson(string json)
    {
        var data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);
        rushDone = data != null && data.done && data.wave == rushWave;
    }

    private void Start()
    {
        FindReferencesIfMissing();

        if (waveManager == null || waveManager.Session == null || panel == null)
        {
            Debug.LogError("SuddenRushController: missing WaveManager or SuddenRushPanel.", this);
            enabled = false;
            return;
        }

        session = waveManager.Session;
        session.WaveStarted += OnWaveStarted;
        session.WaveEnded += OnWaveEnded;
        panel.MoveWorkerChosen += OnMoveWorker;
        panel.UseEnergyChosen += OnUseEnergy;
    }

    private void FindReferencesIfMissing()
    {
        if (waveManager == null) waveManager = FindFirstObjectByType<WaveManager>();
        if (panel == null) panel = FindFirstObjectByType<SuddenRushPanel>(FindObjectsInactive.Include);

        // Nájde nové okno S32 (aj keď je vypnuté)
        if (workerAssign == null)
        {
            var all = FindObjectsByType<WorkerAssignUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var ui in all)
            {
                if (ui.gameObject.name.Contains("32") || ui.transform.Find("Window/BuildingCards") != null)
                {
                    workerAssign = ui;
                    break;
                }
            }
            if (workerAssign == null && all.Length > 0)
                workerAssign = all[0];
        }
    }

    private void OnDestroy()
    {
        if (session != null)
        {
            session.WaveStarted -= OnWaveStarted;
            session.WaveEnded -= OnWaveEnded;
        }
        if (panel != null)
        {
            panel.MoveWorkerChosen -= OnMoveWorker;
            panel.UseEnergyChosen -= OnUseEnergy;
        }
    }

    private void Update()
    {
        if (rushDone || Time.deltaTime <= 0 || GamePersistence.IsRestoring) return;
        if (session == null || session.State != WaveState.Running || session.WaveNumber != rushWave || session.Elapsed < rushAtSeconds) return;
        TriggerRush();
    }

    private void OnWaveStarted(int wave)
    {
        rushDone = false;
        target = null;
        if (panel != null && panel.IsOpen) panel.Close();
    }

    private void OnWaveEnded(int wave)
    {
        target = null;
        if (panel != null && panel.IsOpen) panel.Close();
    }

    private void TriggerRush()
    {
        rushDone = true;
        target = PickBuilding();
        if (target == null) return;
        if (session.EmitRush(target.Need, rushGuests) == 0) return;
        panel.Show(DisplayName(target), BuildingService.BoostEnergyCost, target.transform);
    }

    // Nájde postavenú budovu bez pracovníka (alebo s najmenej pracovníkmi)
    private static BuildingService PickBuilding()
    {
        BuildingService best = null;
        int bestWorkers = int.MaxValue;
        foreach (BuildingService s in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        {
            var info = s.GetComponentInChildren<BuildingInfo>(true);
            if (info == null || !info.isBuilt || s.Need == NeedType.None || s.Seats == 0) continue;
            if (s.Workers >= bestWorkers) continue;
            best = s;
            bestWorkers = s.Workers;
        }
        return best;
    }

    private static string DisplayName(BuildingService s)
    {
        var info = s.GetComponentInChildren<BuildingInfo>(true);
        if (info != null && !string.IsNullOrEmpty(info.buildingName)) return info.buildingName;
        return s.name.Replace("(Clone)", string.Empty).Trim();
    }

    private void OnMoveWorker()
    {
        if (panel != null) panel.Close();

        // 1. Poistka pre cieľovú budovu (aj pri teste cez ContextMenu)
        if (target == null)
            target = PickBuilding();

        // 2. Poistka pre nájdenie S32
        FindReferencesIfMissing();

        if (workerAssign != null)
        {
            BuildingInfo info = target != null ? target.GetComponentInChildren<BuildingInfo>(true) : null;
            if (info == null && target != null) info = target.GetComponentInParent<BuildingInfo>();

            // Otvorí S32
            if (info != null)
                workerAssign.Open(info);
            else if (target != null)
                workerAssign.OpenForNeed(target.Need);
            else
                workerAssign.Open((BuildingInfo)null);

            Debug.Log($"<color=green>[SUDDEN RUSH]</color> Move Worker otvoril S3 pre {DisplayName(target)}");
        }
        else
        {
            Debug.LogError("SuddenRushController: S32 (WorkerAssignUI) sa nenašiel v scéne!", this);
        }
    }

    private void OnUseEnergy()
    {
        if (target == null) target = PickBuilding();
        if (target != null && target.TryBoost() && panel != null)
            panel.Close();
    }
}