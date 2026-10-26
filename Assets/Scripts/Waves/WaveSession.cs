using System;
using System.Collections.Generic;

public enum WaveState { Waiting, Running, Result }

public readonly struct WaveNeed
{
    public int Id { get; }
    public int WaveNumber { get; }
    public NeedType Type { get; }
    public double ArrivalTime { get; }

    public WaveNeed(int id, int waveNumber, NeedType type, double arrivalTime)
    {
        Id = id;
        WaveNumber = waveNumber;
        Type = type;
        ArrivalTime = arrivalTime;
    }
}

// Independent of frame rate and scene objects so timing can be tested deterministically.
public sealed class WaveSession
{
    private static readonly NeedType[] Types =
        { NeedType.Rooms, NeedType.Drinks, NeedType.Food, NeedType.Relax, NeedType.OceanView };
    private readonly WaveCatalog catalog;
    private readonly List<NeedType> arrivals = new();
    private readonly Dictionary<int, WaveNeed> pending = new();
    private readonly HashSet<int> inService = new();
    private int nextId;
    private bool endingArrivals;

    public WaveState State { get; private set; } = WaveState.Waiting;
    public int WaveNumber { get; private set; }
    public int NextWaveNumber => WaveNumber + 1;
    public bool CanStartWave => State != WaveState.Running && WaveNumber < catalog.Count;
    public double ArrivalDuration { get; private set; }
    public double Elapsed { get; private set; }
    public bool ArrivalsEnded { get; private set; }
    public int EmittedCount { get; private set; }
    public int PendingCount => pending.Count;
    public int QueuedCount => pending.Count - inService.Count;
    public int InServiceCount => inService.Count;
    public int ServedCount { get; private set; }
    public IEnumerable<WaveNeed> PendingNeeds => pending.Values;

    public event Action StateChanged;
    public event Action<int> WaveStarted;
    public event Action<WaveNeed> NeedArrived;
    public event Action<int> ArrivalEnded;
    public event Action<int> WaveEnded;

    public WaveSession(WaveCatalog catalog)
    {
        this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
    }

    public bool StartWave(double duration)
    {
        if (!CanStartWave) return false;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        BuildSchedule(catalog.GetWave(WaveNumber));
        WaveNumber++;
        ArrivalDuration = duration;
        Elapsed = 0;
        EmittedCount = 0;
        ArrivalsEnded = false;
        pending.Clear();
        inService.Clear();
        ServedCount = 0;
        State = WaveState.Running;
        StateChanged?.Invoke();
        WaveStarted?.Invoke(WaveNumber);
        return true;
    }

    private void BuildSchedule(WaveDefinition wave)
    {
        arrivals.Clear();
        var emitted = new int[Types.Length];
        var counts = new int[Types.Length];
        for (int i = 0; i < Types.Length; i++)
        {
            counts[i] = wave.GetCount(Types[i]);
            if (counts[i] < 0) throw new ArgumentException("Wave need counts cannot be negative.");
        }

        // Smooth weighted distribution: one need per slot, with exact totals for each type.
        for (int slot = 0; slot < wave.TotalNeeds; slot++)
        {
            int best = -1;
            double bestDeficit = double.NegativeInfinity;
            for (int i = 0; i < Types.Length; i++)
            {
                if (emitted[i] >= counts[i]) continue;
                double deficit = (slot + 1.0) * counts[i] / wave.TotalNeeds - emitted[i];
                if (deficit > bestDeficit) { best = i; bestDeficit = deficit; }
            }
            arrivals.Add(Types[best]);
            emitted[best]++;
        }
    }

    public void Tick(double deltaTime)
    {
        if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (State != WaveState.Running || ArrivalsEnded) return;

        Elapsed = Math.Min(ArrivalDuration, Elapsed + deltaTime);
        // Midpoints keep every scheduled arrival strictly inside the arrival window.
        while (EmittedCount < arrivals.Count)
        {
            double due = (EmittedCount + 0.5) * ArrivalDuration / arrivals.Count;
            if (due > Elapsed) break;
            var need = new WaveNeed(++nextId, WaveNumber, arrivals[EmittedCount++], due);
            pending.Add(need.Id, need);
            NeedArrived?.Invoke(need);
        }

        if (Elapsed >= ArrivalDuration)
        {
            ArrivalsEnded = true;
            endingArrivals = true;
            try { ArrivalEnded?.Invoke(WaveNumber); }
            finally { endingArrivals = false; }
            TryFinishWave();
        }
    }

    // Seating a guest empties a queue slot but must NOT complete their need.
    public bool BeginService(int id)
    {
        return State == WaveState.Running && pending.ContainsKey(id) && inService.Add(id);
    }

    // SCRUM-31: needs never expire. Only completed service can remove them.
    // Unknown IDs, queued guests and duplicate completions are harmless.
    public bool ResolveNeed(int id)
    {
        if (State != WaveState.Running || !inService.Remove(id)) return false;
        pending.Remove(id);
        ServedCount++;
        TryFinishWave();
        return true;
    }

    private void TryFinishWave()
    {
        if (State != WaveState.Running || !ArrivalsEnded || endingArrivals || QueuedCount != 0 || InServiceCount != 0) return;
        State = WaveState.Result;
        int completedWave = WaveNumber;
        WaveEnded?.Invoke(completedWave);
        StateChanged?.Invoke();
    }
}
