using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GameState
{
    public string format = "ParadiseTycoon2D";
    public int version = 2;
    public float timeScale = 1f;
    public List<ResourceSave> resources = new();
    public List<BuildingSave> buildings = new();
    public List<WorkerSave> workers = new();
    public WaveSave wave;
    public bool relaxDebuted, oceanViewDebuted;
    public WaveDefinition lastExecutedWave;
    public GuestPresentationSave presentation;
    public float satisfactionTotal;
    public int satisfactionCount;
    public RewardSave reward;
    public FirstFiveStarRewardSave firstFiveStarReward;
    public List<SaveExtension> extensions = new();

    // Older saves may omit Materials entirely. Do not repair other missing/invalid resources.
    // Called on a deserialized copy, after verifying the original save checksum.
    public void MigrateLegacyResources()
    {
        if (format != "ParadiseTycoon2D" || (version != 1 && version != 2) || resources == null ||
            resources.Count != Enum.GetValues(typeof(ResourceType)).Length - 1) return;
        var types = new HashSet<ResourceType>();
        foreach (var resource in resources)
            if (resource == null || !Enum.IsDefined(typeof(ResourceType), resource.type) ||
                !types.Add(resource.type)) return;
        if (!types.Contains(ResourceType.Materials))
            resources.Add(new ResourceSave { type = ResourceType.Materials, amount = 0 });
    }

    // Permanent progress can be recovered even when transient wave data is no longer compatible.
    public bool IsProgressValid()
    {
        if (format != "ParadiseTycoon2D" || (version != 1 && version != 2) || wave == null || wave.number < 0 ||
            resources == null || resources.Count != Enum.GetValues(typeof(ResourceType)).Length ||
            buildings == null || workers == null || extensions == null) return false;
        var types = new HashSet<ResourceType>();
        foreach (var r in resources)
            if (r == null || !Enum.IsDefined(typeof(ResourceType), r.type) || !types.Add(r.type) || r.amount < 0 ||
                (r.type == ResourceType.Energy && r.amount > ResourceManager.MaxEnergy)) return false;
        var buildingLevels = new Dictionary<string, int>();
        foreach (var b in buildings)
        {
            if (b == null || string.IsNullOrEmpty(b.id) || buildingLevels.ContainsKey(b.id) || !ValidNeed(b.need) ||
                b.level < 1 || b.level > BuildingInfo.MaxLevel) return false;
            buildingLevels[b.id] = b.level;
        }
        var assigned = new Dictionary<string, int>();
        foreach (var w in workers)
        {
            if (w == null) return false;
            if (string.IsNullOrEmpty(w.buildingId)) continue;
            if (!buildingLevels.TryGetValue(w.buildingId, out int level)) return false;
            assigned.TryGetValue(w.buildingId, out int count);
            if (count >= BuildingInfo.DefaultWorkerSlotsForLevel(level)) return false;
            assigned[w.buildingId] = count + 1;
        }
        var keys = new HashSet<string>();
        foreach (var e in extensions) if (e == null || string.IsNullOrEmpty(e.key) || !keys.Add(e.key)) return false;
        return true;
    }

    public bool IsValid()
    {
        if (!IsProgressValid() || wave == null || resources == null ||
            resources.Count != Enum.GetValues(typeof(ResourceType)).Length || buildings == null || workers == null ||
            !Finite(timeScale) || timeScale < 0 || wave.number < 0 || !Enum.IsDefined(typeof(WaveState), wave.state) ||
            wave.arrivals == null || wave.pending == null || wave.inService == null || wave.inTransit == null ||
            wave.servedByType == null || wave.emitted < 0 || wave.emitted > wave.arrivals.Count || wave.served < 0 ||
            wave.served + wave.pending.Count != wave.emitted || wave.vipServed < 0 || wave.vipServed > wave.served || !Finite(wave.elapsed) || !Finite(wave.duration) ||
            wave.elapsed < 0 || wave.duration < 0 || wave.elapsed > wave.duration ||
            !Finite(satisfactionTotal) || satisfactionTotal < 0 || satisfactionCount < 0) return false;
        if (!QueueWaitSave.AreValid(wave.queueWaits)) return false;
        if (!BottleneckSave.AreValid(wave.bottlenecks)) return false;
        var resourceTypes = new HashSet<ResourceType>();
        foreach (var r in resources)
            if (r == null || !Enum.IsDefined(typeof(ResourceType), r.type) || r.amount < 0 || !resourceTypes.Add(r.type) ||
                (r.type == ResourceType.Energy && r.amount > ResourceManager.MaxEnergy)) return false;
        var ids = new HashSet<int>();
        var needs = new Dictionary<int, NeedType>();
        foreach (var n in wave.pending)
        {
            if (n == null || n.id <= 0 || n.wave != wave.number || !ValidNeed(n.type) || !Finite(n.arrival) ||
                n.arrival < 0 || n.id > wave.nextId || !ids.Add(n.id)) return false;
            needs.Add(n.id, n.type);
        }
        int servedTotal = 0;
        var countedTypes = new HashSet<NeedType>();
        foreach (var count in wave.servedByType)
        {
            if (count == null || !ValidNeed(count.type) || count.count < 0 || !countedTypes.Add(count.type)) return false;
            if (count.count > wave.served - servedTotal) return false;
            servedTotal += count.count;
        }
        if (servedTotal != wave.served || wave.nextId < wave.emitted) return false;
        var service = new HashSet<int>();
        foreach (int id in wave.inService) if (!ids.Contains(id) || !service.Add(id)) return false;
        var transit = new HashSet<int>();
        foreach (int id in wave.inTransit) if (!ids.Contains(id) || service.Contains(id) || !transit.Add(id)) return false;
        foreach (var need in wave.arrivals) if (!ValidNeed(need)) return false;
        var buildingLevels = new Dictionary<string, int>();
        var assigned = new Dictionary<string, int>();
        var placed = new HashSet<int>();
        foreach (var b in buildings)
        {
            if (b == null || string.IsNullOrEmpty(b.id) || buildingLevels.ContainsKey(b.id) || b.level < 1 || b.level > BuildingInfo.MaxLevel ||
                b.queue == null || b.approaching == null || b.seats == null || !ValidNeed(b.need)) return false;
            buildingLevels[b.id] = b.level;
            foreach (var q in b.queue)
                if (q == null || !ids.Contains(q.id) || service.Contains(q.id) || transit.Contains(q.id) ||
                    !placed.Add(q.id) || needs[q.id] != b.need || !Finite(q.waited) || q.waited < 0) return false;
            foreach (int id in b.approaching) if (!transit.Contains(id) || !placed.Add(id) || needs[id] != b.need) return false;
            var seatIndices = new HashSet<int>();
            foreach (var seat in b.seats)
                if (seat == null || seat.index < 0 || !service.Contains(seat.id) ||
                    !seatIndices.Add(seat.index) || !placed.Add(seat.id) || needs[seat.id] != b.need ||
                    !Finite(seat.progress) || seat.progress < 0 || seat.progress > 1) return false;
            if (b.boost != null && (!Finite(b.boost.duration) || !Finite(b.boost.remaining) || !Finite(b.boost.time) ||
                !Finite(b.boost.ending) || !Finite(b.boost.clock) || !Finite(b.boost.shift) || b.boost.remaining < 0 ||
                b.boost.duration < 0 || (b.boost.active && b.boost.duration <= 0))) return false;
        }
        if (placed.Count != ids.Count) return false;
        foreach (var w in workers)
        {
            if (w == null) return false;
            if (string.IsNullOrEmpty(w.buildingId)) continue;
            if (!buildingLevels.TryGetValue(w.buildingId, out int level)) return false;
            assigned.TryGetValue(w.buildingId, out int count);
            if (count >= BuildingInfo.DefaultWorkerSlotsForLevel(level)) return false;
            assigned[w.buildingId] = count + 1;
        }
        if (wave.state == WaveState.Running && (wave.number == 0 || wave.duration <= 0 || wave.definition == null)) return false;
        if (reward != null && !reward.IsValid()) return false;
        if (extensions == null) return false;
        var extensionKeys = new HashSet<string>();
        foreach (var extension in extensions)
            if (extension == null || string.IsNullOrEmpty(extension.key) || !extensionKeys.Add(extension.key)) return false;
        if (presentation != null)
        {
            if (presentation.guests == null || !Finite(presentation.boatPosition)) return false;
            var guests = new HashSet<int>();
            foreach (var g in presentation.guests)
            {
                if (g == null || !guests.Add(g.id) || !ValidNeed(g.need) || !Finite(g.position) || !Finite(g.destination) ||
                    g.waypoints == null || g.islandRoute == null || g.serviceApproach == null || g.disembarkSteps < 0 ||
                    !Finite(g.speed) || g.speed <= 0) return false;
                foreach (var p in g.waypoints) if (!Finite(p)) return false;
                foreach (var p in g.islandRoute) if (!Finite(p)) return false;
                foreach (var p in g.serviceApproach) if (!Finite(p)) return false;
            }
            if (wave.state == WaveState.Running)
                foreach (int id in ids) if (!guests.Contains(id)) return false;
        }
        return true;
    }

    private static bool ValidNeed(NeedType value) => value != NeedType.None && Enum.IsDefined(typeof(NeedType), value);
    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Finite(Vector3 p) => Finite(p.x) && Finite(p.y) && Finite(p.z);
}

[Serializable] public sealed class ResourceSave { public ResourceType type; public int amount; }
[Serializable]
public sealed class CountSave
{
    public NeedType type;
    public int count;
}
[Serializable] public sealed class NeedSave
{
    public int id, wave; public NeedType type; public double arrival; public bool isVip;
    public NeedSave() { }
    public NeedSave(WaveNeed need) { id = need.Id; wave = need.WaveNumber; type = need.Type; arrival = need.ArrivalTime; isVip = need.IsVip; }
    public WaveNeed ToNeed() => new(id, wave, type, arrival, isVip);
}
[Serializable] public sealed class QueueSave { public int id; public float waited; }
[Serializable] public sealed class SeatSave { public int index, id; public float progress; }
[Serializable]
public sealed class QueueWaitSave
{
    public NeedType type;
    public float longestWait;
    public float totalWait;
    public int count;

    public static bool AreValid(List<QueueWaitSave> entries)
    {
        if (entries == null) return true; // Legacy saves have no measured waits.
        var types = new HashSet<NeedType>();
        foreach (var entry in entries)
            if (entry == null || entry.type == NeedType.None || !Enum.IsDefined(typeof(NeedType), entry.type) ||
                !types.Add(entry.type) || entry.count <= 0 ||
                float.IsNaN(entry.longestWait) || float.IsInfinity(entry.longestWait) ||
                float.IsNaN(entry.totalWait) || float.IsInfinity(entry.totalWait) ||
                entry.longestWait < 0f || entry.totalWait < entry.longestWait ||
                entry.totalWait > (double)entry.longestWait * entry.count + Math.Max(0.001, entry.totalWait * 0.00001))
                return false;
        return true;
    }
}
[Serializable] public sealed class BuildingSave
{
    public string id, spotId; public int prefabIndex, level; public NeedType need;
    public List<QueueSave> queue = new(); public List<int> approaching = new(); public List<SeatSave> seats = new();
    public BoostSave boost;
    public bool boostUsed;
}
[Serializable] public sealed class WorkerSave { public string buildingId; }
[Serializable] public sealed class WaveSave
{
    public WaveState state; public int number, nextId, emitted, served;
    public int vipServed, vipEveryNthGuest;
    public bool hasVipState; // Missing in legacy saves; keep the existing default cadence for those.
    public double duration, elapsed; public bool arrivalsEnded, holdArrivals, guestReturnPending;
    public WaveDefinition definition;
    public List<NeedType> arrivals = new(); public List<NeedSave> pending = new();
    // SCRUM-395: Cakacie casy jednotlivych potrieb
    public List<QueueWaitSave> queueWaits = new();
    public List<BottleneckSave> bottlenecks = new();
    public List<int> inService = new(), inTransit = new(); public List<CountSave> servedByType = new();
}
[Serializable] public sealed class GuestSave
{
    public int id; public NeedType need; public Vector3 position, destination; public float speed;
    public bool returning, disembarked, boarded, waiting; public int disembarkSteps;
    public List<Vector3> waypoints = new(), islandRoute = new(), serviceApproach = new();
}
[Serializable] public sealed class GuestPresentationSave
{
    public bool boatExists, docked, departing; public Vector3 boatPosition; public List<GuestSave> guests = new();
}
[Serializable] public sealed class BoostSave { public float duration, remaining, time, ending, clock, shift; public bool active; }
[Serializable] public sealed class RewardSave
{
    public bool prepared, claimed; public int waveId; public WaveRewardsData rewards;
    public bool IsValid() => waveId >= -1 &&
        (!prepared || (!claimed && rewards != null)) &&
        (rewards == null || rewards.IsValid());
}
[Serializable] public sealed class SaveExtension { public string key, json; }

/// <summary>Optional modules (VIP, daily rewards, etc.) own their JSON under a stable unique key.</summary>
public interface IGameSaveParticipant
{
    string SaveKey { get; }
    string CaptureSaveJson();
    void RestoreSaveJson(string json);
}

[Serializable] public sealed class FirstFiveStarRewardSave { public bool claimed, legacyHistoryUnknown; }