using System.Collections.Generic;
using UnityEngine;

public sealed partial class SatisfactionTracker : MonoBehaviour
{
    private const float FullSatisfactionSeconds = 10f;
    private const float ZeroSatisfactionSeconds = 40f;

    private WaveSession session;
    private WaveS4Connector connector;
    private readonly List<BuildingService> services = new();
    private float total;
    private int count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureExists()
    {
        if (FindFirstObjectByType<SatisfactionTracker>() == null)
            new GameObject("SatisfactionTracker").AddComponent<SatisfactionTracker>();
    }

    public static float GuestSatisfaction(float waitedSeconds)
    {
        if (waitedSeconds <= FullSatisfactionSeconds) return 1f;
        float range = ZeroSatisfactionSeconds - FullSatisfactionSeconds;
        return Mathf.Clamp01(1f - (waitedSeconds - FullSatisfactionSeconds) / range);
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

        int percent = count > 0 ? Mathf.RoundToInt(total / count * 100f) : 100;
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
