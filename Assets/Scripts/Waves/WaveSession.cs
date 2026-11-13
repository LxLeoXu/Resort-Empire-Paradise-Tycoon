using System;
using System.Collections.Generic;
using UnityEngine;

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

public sealed partial class WaveSession
{
    private static readonly NeedType[] Types =
        { NeedType.Rooms, NeedType.Drinks, NeedType.Food, NeedType.Relax, NeedType.OceanView };
    private readonly WaveCatalog catalog;
    private readonly List<NeedType> arrivals = new();
    private readonly Dictionary<int, WaveNeed> pending = new();
    private readonly HashSet<int> inService = new();
    private readonly HashSet<int> inTransit = new();
    private int nextId;
    private bool endingArrivals;
    private bool guestReturnPending;

    public WaveState State { get; private set; } = WaveState.Waiting;
    public int WaveNumber { get; private set; }
    public int NextWaveNumber => WaveNumber + 1;
    public bool CanStartWave => State != WaveState.Running && WaveNumber < catalog.Count;
    public double ArrivalDuration { get; private set; }
    public double Elapsed { get; private set; }
    public bool ArrivalsEnded { get; private set; }
    public int EmittedCount { get; private set; }
    public int PendingCount => pending.Count;
    public int QueuedCount => pending.Count - inService.Count - inTransit.Count;
    public int InTransitCount => inTransit.Count;
    public int InServiceCount => inService.Count;
    public int ServedCount { get; private set; }
    public int ScheduledCount => arrivals.Count;
    public bool HoldArrivalsOpen { get; set; }
    public IEnumerable<WaveNeed> PendingNeeds => pending.Values;
    public WaveDefinition CurrentWave { get; private set; }

    public event Action<WaveNeed> NeedServed;
    public event Action StateChanged;
    public event Action<int> WaveStarted;
    public event Action<WaveNeed> NeedArrived;
    public event Action<int> ArrivalEnded;
    public event Action<int> WaveEnded;

    public WaveSession(WaveCatalog catalog)
    {
        this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
    }

    public void SetWaveIndex(int index)
    {
        if (State == WaveState.Running) return;
        if (index >= 0 && index < catalog.Count)
        {
            WaveNumber = index;
            State = WaveState.Waiting;
        }
    }

    // 1. Verzia s 2 parametrami pre spätnú kompatibilitu
    public bool StartWave(double duration, ICollection<NeedType> allowedNeeds = null)
    {
        return StartWave(duration, allowedNeeds, null);
    }

    // 2. Verzia s 3 parametrami pre adaptívny mäkký debut
    public bool StartWave(double duration, ICollection<NeedType> allowedNeeds, WaveDefinition waveOverride)
    {
        if (!CanStartWave) return false;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        CurrentWave = waveOverride ?? catalog.GetWave(WaveNumber);

        BuildSchedule(CurrentWave, allowedNeeds);
        WaveNumber++;
        ArrivalDuration = duration;
        Elapsed = 0;
        EmittedCount = 0;
        ArrivalsEnded = false;
        pending.Clear();
        inService.Clear();
        inTransit.Clear();
        ServedCount = 0;
        servedByType.Clear();
        guestReturnPending = false;
        State = WaveState.Running;
        StateChanged?.Invoke();
        WaveStarted?.Invoke(WaveNumber);
        return true;
    }

    private void BuildSchedule(WaveDefinition wave, ICollection<NeedType> allowedNeeds)
    {
        arrivals.Clear();
        var emitted = new int[Types.Length];
        var counts = new int[Types.Length];
        int activeTotalNeeds = 0;

        for (int i = 0; i < Types.Length; i++)
        {
            NeedType type = Types[i];
            int catalogCount = wave.GetCount(type);
            bool isAllowed = allowedNeeds == null || allowedNeeds.Contains(type);

            if (!isAllowed && catalogCount > 0)
            {
                counts[i] = 0;
            }
            else
            {
                counts[i] = catalogCount;
            }

            activeTotalNeeds += counts[i];
        }

        if (activeTotalNeeds == 0) return;

        for (int slot = 0; slot < activeTotalNeeds; slot++)
        {
            int best = -1;
            double bestDeficit = double.NegativeInfinity;
            for (int i = 0; i < Types.Length; i++)
            {
                if (emitted[i] >= counts[i]) continue;
                double deficit = (slot + 1.0) * counts[i] / activeTotalNeeds - emitted[i];
                if (deficit > bestDeficit) { best = i; bestDeficit = deficit; }
            }

            if (best >= 0)
            {
                arrivals.Add(Types[best]);
                emitted[best]++;
            }
        }
    }

    public void Tick(double deltaTime)
    {
        if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (State != WaveState.Running || ArrivalsEnded) return;

        Elapsed = Math.Min(ArrivalDuration, Elapsed + deltaTime);

        if (arrivals.Count == 0 && Elapsed >= ArrivalDuration && !HoldArrivalsOpen)
        {
            ArrivalsEnded = true;
            endingArrivals = true;
            try { ArrivalEnded?.Invoke(WaveNumber); }
            finally { endingArrivals = false; }
            TryFinishWave();
            return;
        }

        while (EmittedCount < arrivals.Count)
        {
            double due = (EmittedCount + 0.5) * ArrivalDuration / arrivals.Count;
            if (due > Elapsed) break;
            var need = new WaveNeed(++nextId, WaveNumber, arrivals[EmittedCount++], due);
            pending.Add(need.Id, need);
            NeedArrived?.Invoke(need);
        }

        if (Elapsed >= ArrivalDuration && !HoldArrivalsOpen)
        {
            ArrivalsEnded = true;
            endingArrivals = true;
            try { ArrivalEnded?.Invoke(WaveNumber); }
            finally { endingArrivals = false; }
            TryFinishWave();
        }
    }

    public bool BeginService(int id)
    {
        return State == WaveState.Running && pending.ContainsKey(id) && !inTransit.Contains(id) && inService.Add(id);
    }

    public bool MarkInTransit(int id) => State == WaveState.Running && pending.ContainsKey(id)
        && !inService.Contains(id) && inTransit.Add(id);

    public bool JoinQueue(int id) => State == WaveState.Running && inTransit.Remove(id);

    public int GetQueueCount(NeedType type)
    {
        int count = 0;
        foreach (var need in pending.Values)
            if (need.Type == type && !inTransit.Contains(need.Id) && !inService.Contains(need.Id)) count++;
        return count;
    }

    public bool ResolveNeed(int id)
    {
        if (State == WaveState.Running && inService.Remove(id))
        {
            if (pending.TryGetValue(id, out var need))
            {
                pending.Remove(id);
                ServedCount++;
                servedByType.TryGetValue(need.Type, out int served);
                servedByType[need.Type] = served + 1;
                NeedServed?.Invoke(need);
                TryFinishWave();
                return true;
            }
        }
        return false;
    }

    public void SetGuestReturnPending(bool value)
    {
        guestReturnPending = value;
        if (!value) TryFinishWave();
    }

    private void TryFinishWave()
    {
        if (State != WaveState.Running || !ArrivalsEnded || endingArrivals || guestReturnPending || pending.Count != 0) return;
        State = WaveState.Result;
        int completedWave = WaveNumber;
        WaveEnded?.Invoke(completedWave);
        StateChanged?.Invoke();
    }

    public WaveDefinition GetCurrentWaveDefinition()
    {
        if (CurrentWave != null) return CurrentWave;
        int index = Math.Clamp(WaveNumber - 1, 0, catalog.Count - 1);
        return catalog.GetWave(index);
    }
}
