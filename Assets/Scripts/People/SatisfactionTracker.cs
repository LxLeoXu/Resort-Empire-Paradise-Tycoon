using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class SatisfactionTracker : MonoBehaviour
{
    [Header("Tuning Trpezlivosti")]
    public float patienceGrace = 10f;
    public float patienceFloorAfter = 10f;
    public float unitFloor = 0.0f;
    public float waveFloor = 0.40f;

    [Header("Tuning Kvality Budov (Lv 4–5)")]
    [Tooltip("Násobič bonusu kvality z budovy (BuildingInfo -> Quality Bonus By Level). 1.0 = použije hodnoty z budovy.")]
    [Range(0f, 3f)] public float qualityBonusScale = 1f;

    private WaveSession session;
    private WaveS4Connector connector;
    private readonly List<(BuildingService service, Action<WaveNeed, int, float> handler)> serviceSubscriptions = new();
    private float total;
    private int count;

    public float LiveSatisfactionPercent
    {
        get
        {
            if (count == 0) return 100f;
            float raw = total / count;
            return Mathf.Max(waveFloor, raw) * 100f;
        }
    }
    public int LiveServedCount => count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureExists()
    {
        if (FindFirstObjectByType<SatisfactionTracker>() == null)
            new GameObject("SatisfactionTracker").AddComponent<SatisfactionTracker>();
    }

    public float GuestSatisfaction(float waitedSeconds)
    {
        // 1. Do patienceGrace (10s) je spokojnosť plných 100 %
        if (waitedSeconds <= patienceGrace) return 1f;

        // 2. Nad patienceFloorAfter (30s) narazí na dno (unitFloor)
        if (waitedSeconds >= patienceFloorAfter) return unitFloor;

        // 3. Medzi patienceGrace a patienceFloorAfter klesá lineárne
        float t = (waitedSeconds - patienceGrace) / Mathf.Max(0.01f, patienceFloorAfter - patienceGrace);
        return Mathf.Lerp(1f, unitFloor, t);
    }

    public float GuestSatisfaction(float waitedSeconds, BuildingInfo info)
    {
        float baseSatisfaction = GuestSatisfaction(waitedSeconds);
        if (info == null || info.IsSpa) return baseSatisfaction;

        float qualityBonus = Mathf.Max(0f, info.CurrentQualityBonus * qualityBonusScale);
        return Mathf.Clamp01(baseSatisfaction + qualityBonus);
    }

    private void Start()
    {
        WaveManager manager = FindFirstObjectByType<WaveManager>();
        if (manager == null || manager.Session == null)
        {
            Debug.LogError("SatisfactionTracker: WaveManager not found.", this);
            enabled = false;
            return;
        }

        connector = FindFirstObjectByType<WaveS4Connector>(FindObjectsInactive.Include);
        if (connector == null)
            Debug.LogWarning("SatisfactionTracker: WaveS4Connector not found, satisfaction will not reach S4.", this);

        session = manager.Session;
        session.WaveStarted += OnWaveStarted;
        session.WaveEnded += OnWaveEnded;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (session == null) return;
        session.WaveStarted -= OnWaveStarted;
        session.WaveEnded -= OnWaveEnded;
    }

    private void OnWaveStarted(int wave)
    {
        total = 0f;
        count = 0;
        SubscribeToServices();
    }

    private void SubscribeToServices()
    {
        Unsubscribe();
        BuildingService[] found = FindObjectsByType<BuildingService>(FindObjectsSortMode.None);
        foreach (BuildingService s in found)
        {
            if (s == null) continue;
            BuildingService captured = s;
            Action<WaveNeed, int, float> handler = (need, seatIndex, waitedSeconds) =>
                OnSeated(captured, need, seatIndex, waitedSeconds);
            captured.Seated += handler;
            serviceSubscriptions.Add((captured, handler));
        }
    }

    private void OnSeated(BuildingService service, WaveNeed need, int seatIndex, float waitedSeconds)
    {
        BuildingInfo info = service != null ? service.GetComponentInChildren<BuildingInfo>(true) : null;
        if (info == null && service != null) info = service.GetComponentInParent<BuildingInfo>();

        total += GuestSatisfaction(waitedSeconds, info);
        count++;
    }

    private void OnWaveEnded(int wave)
    {
        Unsubscribe();

        float rawPercent = count > 0 ? (total / count * 100f) : 100f;
        int percent = Mathf.RoundToInt(Mathf.Max(waveFloor * 100f, rawPercent));
        Debug.Log($"[SPOKOJNOST] Vlna {wave}: {percent}% ({count} hostí)");

        if (connector != null)
            connector.SetSatisfactionPercent(percent);
    }

    private void Unsubscribe()
    {
        foreach (var entry in serviceSubscriptions)
        {
            if (entry.service != null && entry.handler != null)
                entry.service.Seated -= entry.handler;
        }
        serviceSubscriptions.Clear();
    }
}