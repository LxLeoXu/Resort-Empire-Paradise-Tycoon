using System.Collections.Generic;
using UnityEngine;

public sealed partial class SatisfactionTracker : MonoBehaviour
{
    [Header("Tuning Trpezlivosti")]
    public float patienceGrace = 10f;
    public float patienceFloorAfter = 10f;
    public float unitFloor = 0.0f;
    public float waveFloor = 0.40f;

    private WaveSession session;
    private WaveS4Connector connector;
    private readonly List<BuildingService> services = new();
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

        // 2. Nad patienceFloorAfter (30s) narazí na dno (25 %)
        if (waitedSeconds >= patienceFloorAfter) return unitFloor;

        // 3. Medzi 10s a 30s klesá lineárne zo 100 % na 25 %
        float t = (waitedSeconds - patienceGrace) / Mathf.Max(0.01f, patienceFloorAfter - patienceGrace);
        return Mathf.Lerp(1f, unitFloor, t);
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
        Unsubscribe();
        total = 0f;
        count = 0;

        BuildingService[] found = FindObjectsByType<BuildingService>(FindObjectsSortMode.None);
        foreach (BuildingService s in found)
        {
            s.Seated += OnSeated;
            services.Add(s);
        }
    }

    private void OnSeated(WaveNeed need, int seatIndex, float waitedSeconds)
    {
        total += GuestSatisfaction(waitedSeconds);
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
        foreach (BuildingService s in services)
            if (s != null) s.Seated -= OnSeated;
        services.Clear();
    }
}