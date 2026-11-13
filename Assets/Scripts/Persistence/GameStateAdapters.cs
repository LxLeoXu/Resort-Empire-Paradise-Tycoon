using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WaveSession
{
    private readonly Dictionary<NeedType, int> servedByType = new();
    public event Action StateRestored;
    public int GetServedCount(NeedType need) => servedByType.TryGetValue(need, out int count) ? count : 0;
    public bool TryGetNeed(int id, out WaveNeed need) => pending.TryGetValue(id, out need);
    public WaveSave CaptureState()
    {
        var data = new WaveSave { state = State, number = WaveNumber, nextId = nextId, emitted = EmittedCount,
            served = ServedCount, duration = ArrivalDuration, elapsed = Elapsed, arrivalsEnded = ArrivalsEnded,
            holdArrivals = HoldArrivalsOpen, guestReturnPending = guestReturnPending, definition = CurrentWave,
            arrivals = new List<NeedType>(arrivals), inService = new List<int>(inService), inTransit = new List<int>(inTransit) };
        foreach (var need in pending.Values) data.pending.Add(new NeedSave(need));
        foreach (var entry in servedByType) data.servedByType.Add(new CountSave { type = entry.Key, count = entry.Value });
        return data;
    }
    public void RestoreState(WaveSave data)
    {
        if (data.number > catalog.Count) throw new InvalidOperationException("Saved wave is not in the catalog.");
        State = data.state; WaveNumber = data.number; nextId = data.nextId; EmittedCount = data.emitted;
        ServedCount = data.served; ArrivalDuration = data.duration; Elapsed = data.elapsed; ArrivalsEnded = data.arrivalsEnded;
        HoldArrivalsOpen = data.holdArrivals; guestReturnPending = data.guestReturnPending; CurrentWave = data.definition;
        arrivals.Clear(); arrivals.AddRange(data.arrivals);
        pending.Clear(); foreach (var need in data.pending) pending.Add(need.id, need.ToNeed());
        inService.Clear(); foreach (int id in data.inService) inService.Add(id);
        inTransit.Clear(); foreach (int id in data.inTransit) inTransit.Add(id);
        servedByType.Clear(); foreach (var count in data.servedByType) servedByType.Add(count.type, count.count);
        endingArrivals = false;
    }
    public void NotifyStateRestored() { StateChanged?.Invoke(); StateRestored?.Invoke(); }
}

public sealed partial class WaveManager
{
    public void CaptureProgress(GameState data)
    {
        data.wave = Session.CaptureState(); data.relaxDebuted = hasRelaxDebuted;
        data.oceanViewDebuted = hasOceanViewDebuted; data.lastExecutedWave = lastExecutedWave;
    }
    public void RestoreProgress(GameState data)
    {
        useTestWave = false; hasRelaxDebuted = data.relaxDebuted; hasOceanViewDebuted = data.oceanViewDebuted;
        lastExecutedWave = data.lastExecutedWave; Session.RestoreState(data.wave);
    }
    public void StartFreshProgress() { useTestWave = false; Session.SetWaveIndex(0); }
}

public partial class ResourceManager
{
    public List<ResourceSave> CaptureResources()
    {
        var data = new List<ResourceSave>();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) data.Add(new ResourceSave { type = type, amount = GetResource(type) });
        return data;
    }
    public void RestoreResources(List<ResourceSave> data)
    {
        resources.Clear();
        foreach (var r in data) { resources[r.type] = r.amount; OnResourceChanged?.Invoke(r.type, r.amount); }
    }
}

public sealed partial class BuildingService
{
    public BuildingSave CaptureState(string id)
    {
        var data = new BuildingSave { id = id, need = Need, level = info.currentLevel };
        foreach (var entry in queue) data.queue.Add(new QueueSave { id = entry.need.Id, waited = Mathf.Max(0, Time.time - entry.queuedAt) });
        foreach (var need in approaching) data.approaching.Add(need.Id);
        for (int i = 0; i < seats.Length; i++)
            if (seats[i].Occupied) data.seats.Add(new SeatSave { index = i, id = seats[i].Need.Id, progress = seats[i].Progress });
        var boost = GetComponent<SpeedBoostEffect>();
        if (boost != null) data.boost = boost.CaptureState();
        return data;
    }
    public void RestoreState(BuildingSave data)
    {
        EnsureSeats(); queue.Clear(); approaching.Clear();
        foreach (var seat in seats) { seat.Occupied = false; seat.Progress = 0; }
        foreach (var entry in data.queue)
        {
            if (!session.TryGetNeed(entry.id, out var need)) throw new InvalidOperationException("Queued need is missing.");
            queue.Enqueue((need, Time.time - entry.waited));
        }
        foreach (int id in data.approaching)
        {
            if (!session.TryGetNeed(id, out var need)) throw new InvalidOperationException("Incoming need is missing.");
            approaching.Enqueue(need);
        }
        foreach (var saved in data.seats)
        {
            if (saved.index >= Seats || !session.TryGetNeed(saved.id, out var need)) throw new InvalidOperationException("Saved seat is unavailable.");
            seats[saved.index].Occupied = true; seats[saved.index].Need = need; seats[saved.index].Progress = saved.progress;
        }
        if (data.boost != null) GetComponent<SpeedBoostEffect>()?.RestoreState(data.boost);
        Changed?.Invoke();
    }
}

public partial class BuildingSpot
{
    private int builtPrefabIndex = -1;
    public GameObject SpawnedBuilding => spawnedBuilding;
    public int BuiltPrefabIndex => builtPrefabIndex;
    public GameObject RestoreBuilding(int index)
    {
        if (spawnedBuilding != null) return spawnedBuilding;
        if (index < 0 || index >= buildingPrefabs.Count || buildingPrefabs[index] == null)
            throw new InvalidOperationException("Saved building prefab is unavailable.");
        spawnedBuilding = Instantiate(buildingPrefabs[index], transform.position, Quaternion.identity);
        builtPrefabIndex = index; isOccupied = true; gameObject.SetActive(false);
        return spawnedBuilding; // Loading neither charges coins nor grants ResortValue a second time.
    }
}

public sealed partial class WorkerRoster
{
    public List<WorkerSave> CaptureState(Dictionary<BuildingService, string> buildings)
    {
        var result = new List<WorkerSave>();
        foreach (var w in workers) result.Add(new WorkerSave { name = w.displayName, stars = w.stars,
            buildingId = w.building != null && buildings.TryGetValue(w.building, out var id) ? id : null });
        return result;
    }
    public void RestoreState(List<WorkerSave> data, Dictionary<string, BuildingService> buildings)
    {
        foreach (var b in buildings.Values) b.SetWorkers(0);
        workers.Clear();
        foreach (var saved in data)
        {
            var w = new Worker(saved.name, saved.stars); workers.Add(w);
            if (!string.IsNullOrEmpty(saved.buildingId)) { w.building = buildings[saved.buildingId]; w.building.SetWorkers(CountIn(w.building)); }
        }
        Changed?.Invoke();
    }
}

public sealed partial class GuestAgent
{
    public GuestSave CaptureState(int id, NeedType need) => new()
    {
        id = id, need = need, position = transform.position, destination = destination, speed = speed,
        returning = returningToBoat, disembarked = Disembarked, boarded = Boarded, waiting = IsWaitingInQueue,
        disembarkSteps = disembarkStepsRemaining, waypoints = new List<Vector3>(waypoints),
        islandRoute = new List<Vector3>(islandRoute), serviceApproach = new List<Vector3>(serviceApproach)
    };
    public void RestoreState(GuestSave data)
    {
        transform.position = data.position; destination = data.destination; speed = data.speed; returningToBoat = data.returning;
        Disembarked = data.disembarked; Boarded = data.boarded; IsWaitingInQueue = data.waiting; disembarkStepsRemaining = data.disembarkSteps;
        waypoints.Clear(); foreach (var p in data.waypoints) waypoints.Enqueue(p);
        islandRoute = data.islandRoute.ToArray(); serviceApproach = data.serviceApproach.ToArray();
        gameObject.SetActive(!Boarded);
    }
}

public sealed partial class GuestWavePresentation
{
    private readonly Dictionary<int, NeedType> guestTypes = new();
    public GuestPresentationSave CaptureState()
    {
        var data = new GuestPresentationSave { boatExists = boat != null, boatPosition = boat != null ? boat.transform.position : Vector3.zero,
            docked = BoatDocked, departing = departing };
        foreach (var entry in guests) if (entry.Value != null) data.guests.Add(entry.Value.CaptureState(entry.Key, guestTypes[entry.Key]));
        return data;
    }
    public void RestoreState(GuestPresentationSave data)
    {
        UnsubscribeServices();
        foreach (var service in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        { service.Seated += OnSeated; service.ServiceCompleted += OnServiceCompleted; services.Add(service); }
        if (visualRoot == null) visualRoot = new GameObject("WaveGuests").transform;
        foreach (Transform child in visualRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        guests.Clear(); guestTypes.Clear(); boat = null;
        BoatDocked = data.docked; departing = data.departing;
        if (data.boatExists) boat = Instantiate(boatPrefab, data.boatPosition, Quaternion.identity, visualRoot);
        foreach (var saved in data.guests)
        {
            var instance = Instantiate(guestPrefab, saved.position, Quaternion.identity, visualRoot);
            instance.name = $"Guest_{saved.id}_{saved.need}";
            var guest = instance.GetComponent<GuestAgent>(); guest.RestoreState(saved);
            guests.Add(saved.id, guest); guestTypes.Add(saved.id, saved.need);
        }
    }
}

public sealed partial class SatisfactionTracker
{
    public void CaptureState(GameState data) { data.satisfactionTotal = total; data.satisfactionCount = count; }
    public int CurrentPercent => count > 0 ? Mathf.RoundToInt(total / count * 100f) : 100;
    public void RestoreState(GameState data)
    {
        total = data.satisfactionTotal; count = data.satisfactionCount; Unsubscribe();
        foreach (var service in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        { service.Seated += OnSeated; services.Add(service); }
    }
}

public partial class ClaimRewards
{
    public bool HasPendingReward => rewardPrepared && !claimed && pendingRewards != null;
    public RewardSave CaptureState() => new() { prepared = rewardPrepared, claimed = claimed, waveId = lastWaveId, rewards = pendingRewards };
    public void RestoreState(RewardSave data)
    {
        pendingRewards = data.rewards; rewardPrepared = data.prepared; claimed = data.claimed; lastWaveId = data.waveId; RefreshButton();
    }
}

public sealed partial class SpeedBoostEffect
{
    public BoostSave CaptureState() => new() { duration = duration, remaining = remaining, time = time,
        ending = ending, clock = clock, shift = shift, active = effect != null && effect.gameObject.activeSelf };
    public void RestoreState(BoostSave data)
    {
        duration = data.duration; remaining = data.remaining; time = data.time; ending = data.ending; clock = data.clock; shift = data.shift;
        if (effect == null) return;
        Capture(); effect.gameObject.SetActive(data.active); if (data.active) Apply();
    }
}
