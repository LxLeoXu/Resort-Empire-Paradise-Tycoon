using System.Collections.Generic;
using UnityEngine;

// SCRUM-395 + SCRUM-431
// Statistiky cakania hosti pocas vlny.
public sealed partial class WaveSession
{
    private readonly Dictionary<NeedType, float> longestWaitByType = new();
    private readonly Dictionary<NeedType, float> totalWaitByType = new();
    private readonly Dictionary<NeedType, int> waitCountByType = new();

    public void RecordQueueWait(NeedType type, float seconds)
    {
        if (State != WaveState.Running || type == NeedType.None || !System.Enum.IsDefined(typeof(NeedType), type) ||
            float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
            return;

        float wait = Mathf.Max(0f, seconds);

        if (!longestWaitByType.ContainsKey(type))
        {
            longestWaitByType[type] = 0f;
            totalWaitByType[type] = 0f;
            waitCountByType[type] = 0;
        }

        if (waitCountByType[type] == int.MaxValue || float.IsInfinity(totalWaitByType[type] + wait)) return;
        longestWaitByType[type] =
            Mathf.Max(longestWaitByType[type], wait);

        totalWaitByType[type] += wait;
        waitCountByType[type]++;
    }

    public float GetLongestQueueWait(NeedType type)
    {
        return longestWaitByType.TryGetValue(type, out float value)
            ? value
            : 0f;
    }

    public float GetAverageQueueWait(NeedType type)
    {
        if (!waitCountByType.TryGetValue(type, out int count) ||
            count == 0)
            return 0f;

        return totalWaitByType[type] / count;
    }

    public int GetQueueWaitSampleCount(NeedType type)
    {
        return waitCountByType.TryGetValue(type, out int count)
            ? count
            : 0;
    }

    private void ResetQueueWaitStatistics()
    {
        longestWaitByType.Clear();
        totalWaitByType.Clear();
        waitCountByType.Clear();
    }
    // SCRUM-395: Priprava statistik na ulozenie
public List<QueueWaitSave> CaptureQueueWaitStatistics()
{
    List<QueueWaitSave> result = new();

    foreach (var entry in waitCountByType)
    {
        NeedType type = entry.Key;

        result.Add(new QueueWaitSave
        {
            type = type,
            longestWait = GetLongestQueueWait(type),
            totalWait = totalWaitByType[type],
            count = entry.Value
        });
    }

    return result;
}

// SCRUM-395: Obnovenie statistik z ulozenej hry
public void RestoreQueueWaitStatistics(
    List<QueueWaitSave> savedStatistics)
{
    ResetQueueWaitStatistics();

    // Starsie ulozene hry nemusia mat tieto udaje.
    if (savedStatistics == null)
        return;

    if (!QueueWaitSave.AreValid(savedStatistics))
        throw new System.InvalidOperationException("Invalid queue wait statistics.");
    foreach (QueueWaitSave entry in savedStatistics)
    {
        longestWaitByType[entry.type] = entry.longestWait;
        totalWaitByType[entry.type] = entry.totalWait;
        waitCountByType[entry.type] = entry.count;
    }
}
// SCRUM-431: Najlepsie obsluzena potreba
public NeedType GetBestServedNeedType()
{
    NeedType bestType = NeedType.None;

    int bestServed = 0;
    int bestTotal = 1;

    NeedType[] types =
    {
        NeedType.Rooms,
        NeedType.Drinks,
        NeedType.Food,
        NeedType.Relax,
        NeedType.OceanView
    };

    foreach (NeedType type in types)
    {
        int total = 0;

        for (int i = 0; i < EmittedCount; i++)
        {
            if (arrivals[i] == type)
                total++;
        }

        if (total == 0)
            continue;

        int served = GetServedCount(type);
        if (served <= 0) continue;

        // Porovnanie uspesnosti obsluhy.
        // Pri rovnakej uspesnosti vyhra vyssi pocet
        // obsluzenych potrieb.
        if (bestType == NeedType.None ||
            (long)served * bestTotal > (long)bestServed * total ||
            ((long)served * bestTotal == (long)bestServed * total &&
             served > bestServed))
        {
            bestType = type;
            bestServed = served;
            bestTotal = total;
        }
    }

    return bestType;
}
// SCRUM-395: Budova s najdlhsim problemovym cakanim
public NeedType GetMostDelayedNeed(float thresholdSeconds)
{
    NeedType worstType = NeedType.None;

    if (float.IsNaN(thresholdSeconds) || float.IsInfinity(thresholdSeconds)) return NeedType.None;
    thresholdSeconds = Mathf.Max(0f, thresholdSeconds);
    float longestWait = -1f;

    NeedType[] types =
    {
        NeedType.Rooms,
        NeedType.Drinks,
        NeedType.Food,
        NeedType.Relax,
        NeedType.OceanView
    };

    foreach (NeedType type in types)
    {
        float wait = GetLongestQueueWait(type);

        if (GetQueueWaitSampleCount(type) > 0 && wait > 0f && wait >= thresholdSeconds &&
            wait > longestWait)
        {
            longestWait = wait;
            worstType = type;
        }
    }

    return worstType;
}
}