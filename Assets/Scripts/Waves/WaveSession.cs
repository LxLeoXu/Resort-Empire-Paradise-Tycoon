using System;
using System.Collections.Generic;
using UnityEngine; // Potrebné pre Debug.Log

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
    public event Action<WaveNeed> NeedServed; // Udalosť pri obslúžení potreby

    public event Action StateChanged;
    public event Action<int> WaveStarted;
    public event Action<WaveNeed> NeedArrived;
    public event Action<int> ArrivalEnded;
    public event Action<int> WaveEnded;

    public WaveSession(WaveCatalog catalog)
    {
        this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>
    /// SCRUM-25 DEBUG: Umožní priamo skočiť na vlnu (index od 0, napr. 3 = Vlna 4)
    /// </summary>
    public void SetWaveIndex(int index)
    {
        if (index >= 0 && index < catalog.Count)
        {
            WaveNumber = index;
            State = WaveState.Waiting;
        }
    }

    public bool StartWave(double duration, ICollection<NeedType> allowedNeeds = null)
    {
        if (!CanStartWave) return false;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0)
            throw new ArgumentOutOfRangeException(nameof(duration));

        BuildSchedule(catalog.GetWave(WaveNumber), allowedNeeds);
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

    private void BuildSchedule(WaveDefinition wave, ICollection<NeedType> allowedNeeds)
    {
        arrivals.Clear();
        var emitted = new int[Types.Length];
        var counts = new int[Types.Length];
        int activeTotalNeeds = 0;

        Debug.Log($"<color=cyan>--- ŠTART VLNY {WaveNumber + 1} (Kontrola budov a potrieb) ---</color>");

        for (int i = 0; i < Types.Length; i++)
        {
            NeedType type = Types[i];
            int catalogCount = wave.GetCount(type);
            bool isAllowed = allowedNeeds == null || allowedNeeds.Contains(type);

            if (!isAllowed && catalogCount > 0)
            {
                // Budova nie je postavená, potreba sa vynechá (Pravidlo WR9)
                counts[i] = 0;
                Debug.LogWarning($"[WR9 FILTER] Potreba <color=orange>{type}</color> ({catalogCount} ks) bola VYRADENÁ, lebo budova nestojí!");
            }
            else
            {
                counts[i] = catalogCount;
                if (counts[i] > 0)
                {
                    Debug.Log($"[POVOLENÉ] Potreba <b>{type}</b>: do vlny vstúpi {counts[i]} ks.");
                }
            }

            activeTotalNeeds += counts[i];
        }

        Debug.Log($"<color=cyan>Celkový počet potrieb pre túto vlnu: {activeTotalNeeds}</color>");

        if (activeTotalNeeds == 0) return;

        // Distribúcia v čase
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

        if (arrivals.Count == 0 && Elapsed >= ArrivalDuration)
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

            // Výpis každej prichádzajúcej potreby v reálnom čase
            Debug.Log($"--> Prichádza: {need.Type} (Vlna {need.WaveNumber})");
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

    public bool BeginService(int id)
    {
        return State == WaveState.Running && pending.ContainsKey(id) && inService.Add(id);
    }


    public bool ResolveNeed(int id)
    {
        if (State != WaveState.Running || !inService.Remove(id)) return false;

        if (pending.TryGetValue(id, out var need))
        {
            pending.Remove(id);
            ServedCount++;
            NeedServed?.Invoke(need); // <-- TOTO SEM PRIDAJ
            TryFinishWave();
            return true;
        }

        return false;
    }

    private void TryFinishWave()
    {
        if (State != WaveState.Running || !ArrivalsEnded || endingArrivals || QueuedCount != 0 || InServiceCount != 0) return;
        State = WaveState.Result;
        int completedWave = WaveNumber;
        WaveEnded?.Invoke(completedWave);
        StateChanged?.Invoke();
    }
    public WaveDefinition GetCurrentWaveDefinition()
    {
        int index = Math.Clamp(WaveNumber - 1, 0, catalog.Count - 1);
        return catalog.GetWave(index);
    }
}