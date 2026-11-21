using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BottleneckSave
{
    public NeedType type;
    public float duration, delayElapsed;
    public bool active;
    public static bool AreValid(List<BottleneckSave> data)
    {
        if (data == null) return true; // Legacy saves.
        var ids = new HashSet<NeedType>();
        foreach (var entry in data)
            if (entry == null || entry.type == NeedType.None || !Enum.IsDefined(typeof(NeedType), entry.type) || !ids.Add(entry.type) ||
                float.IsNaN(entry.duration) || float.IsInfinity(entry.duration) || entry.duration < 0 ||
                float.IsNaN(entry.delayElapsed) || float.IsInfinity(entry.delayElapsed) || entry.delayElapsed < 0) return false;
        return true;
    }
}

public sealed partial class WaveSession
{
    private readonly Dictionary<NeedType, BottleneckSave> bottlenecks = new();
    public bool HasBottleneckMeasurements => bottlenecks.Count > 0;
    // SCRUM-451: S4 "Needs improvement" and S5 share the same measured result.
    public event Action<NeedType> RecommendationReady;
    public bool IsBottleneck(NeedType type) => bottlenecks.TryGetValue(type, out var value) && value.active;
    public float GetBottleneckDuration(NeedType type) => bottlenecks.TryGetValue(type, out var value) ? value.duration : 0;

    // ---- RECOVERING (transient, not saved) ----
    private sealed class RecoveryState
    {
        public bool requested, recovering;
        public int refCount;
        public float growTime;
    }
    private readonly Dictionary<NeedType, RecoveryState> recoveries = new();

    private RecoveryState GetRecovery(NeedType type)
    {
        if (!recoveries.TryGetValue(type, out var r)) recoveries[type] = r = new RecoveryState();
        return r;
    }

    public bool IsRecovering(NeedType type) => recoveries.TryGetValue(type, out var r) && r.recovering;

    /// Called when a worker arrives at a building. Recovery only starts if the queue is already over the threshold.
    public void RequestRecovery(NeedType type)
    {
        if (State != WaveState.Running || type == NeedType.None) return;
        GetRecovery(type).requested = true;
    }

    public void ClearRecovery() => recoveries.Clear();

        // Compatibility overload for old callers (e.g. Assets/Editor/ProgressionVerification.cs).
    // The queue size is unknown here, so recovery tracking is not used for these calls.
    public void UpdateBottleneck(NeedType type, float oldestWait, float deltaTime, float threshold, float delay)
        => UpdateBottleneck(type, oldestWait, 0, deltaTime, threshold, delay);

    public void UpdateBottleneck(NeedType type, float oldestWait, int queueCount, float deltaTime,
        float threshold, float delay, float regrowSeconds = 3f)
    {
        if (State != WaveState.Running || type == NeedType.None || !Enum.IsDefined(typeof(NeedType), type) ||
            !Finite(oldestWait) || !Finite(deltaTime) || !Finite(threshold) || !Finite(delay) || deltaTime <= 0) return;
        if (!bottlenecks.TryGetValue(type, out var state)) bottlenecks[type] = state = new BottleneckSave { type = type };

        bool over = oldestWait > 0 && oldestWait >= Mathf.Max(0, threshold);
        var rec = GetRecovery(type);

        // Worker just moved in: enter RECOVERING only if the queue is really over the threshold.
        if (rec.requested)
        {
            rec.requested = false;
            if (over)
            {
                rec.recovering = true;
                rec.refCount = queueCount;
                rec.growTime = 0f;
                state.active = false;
                state.delayElapsed = 0f;
            }
        }

        if (rec.recovering)
        {
            if (!over)
            {
                // Queue dropped to the threshold -> OK.
                rec.recovering = false;
                state.active = false;
                state.delayElapsed = 0f;
                return;
            }

            if (queueCount < rec.refCount) { rec.refCount = queueCount; rec.growTime = 0f; }   // still shrinking
            else if (queueCount > rec.refCount)
            {
                rec.growTime += deltaTime;                                                     // growing again
                if (rec.growTime >= regrowSeconds)
                {
                    rec.recovering = false;                                                    // -> BOTTLENECK
                    state.delayElapsed = Mathf.Max(0, delay);                                  // no extra delay
                }
            }
            else rec.growTime = 0f;

            if (rec.recovering) return;   // while recovering, neither BOTTLENECK nor its duration is counted
        }

        if (over)
        {
            state.delayElapsed += deltaTime;
            state.active = state.delayElapsed >= Mathf.Max(0, delay);
            if (state.active) state.duration += deltaTime;
        }
        else { state.delayElapsed = 0; state.active = false; }
    }

    public NeedType GetRecommendedNeed()
    {
        NeedType best = NeedType.None;
        float longest = 0;
        foreach (NeedType type in Enum.GetValues(typeof(NeedType)))
        {
            float duration = GetBottleneckDuration(type);
            if (duration > longest) { longest = duration; best = type; }
        }
        return best;
    }
    public List<BottleneckSave> CaptureBottlenecks()
    {
        var result = new List<BottleneckSave>();
        foreach (var state in bottlenecks.Values) result.Add(new BottleneckSave
            { type = state.type, duration = state.duration, delayElapsed = state.delayElapsed, active = state.active });
        return result;
    }
    public void RestoreBottlenecks(List<BottleneckSave> data)
    {
        bottlenecks.Clear();
        recoveries.Clear();
        if (data == null) return;
        if (!BottleneckSave.AreValid(data)) throw new ArgumentException("Invalid bottleneck statistics.");
        foreach (var saved in data)
        {
            if (saved == null || saved.type == NeedType.None || !Enum.IsDefined(typeof(NeedType), saved.type) ||
                !Finite(saved.duration) || !Finite(saved.delayElapsed) || saved.duration < 0 || saved.delayElapsed < 0 || bottlenecks.ContainsKey(saved.type))
                throw new ArgumentException("Invalid bottleneck statistics.");
            bottlenecks.Add(saved.type, new BottleneckSave { type = saved.type, duration = saved.duration,
                delayElapsed = saved.delayElapsed, active = saved.active });
        }
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}