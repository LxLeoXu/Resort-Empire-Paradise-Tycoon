using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorkerRoster : MonoBehaviour
{
    public const int MaxPerBuilding = 3;

    [Serializable]
    public sealed class Worker
    {
        public string displayName;
        [Range(0, 5)] public int stars;
        [NonSerialized] public BuildingService building;
        public bool IsFree => building == null;

        public Worker(string displayName, int stars)
        {
            this.displayName = displayName;
            this.stars = stars;
        }
    }

    [SerializeField] private List<Worker> workers = new()
    {
        new Worker("Chef Marco", 4),
        new Worker("Chef Lina", 2),
        new Worker("Chef Leo", 4),
    };

    private static WorkerRoster instance;

    public static WorkerRoster Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<WorkerRoster>();
            if (instance == null)
                instance = new GameObject("WorkerRoster").AddComponent<WorkerRoster>();
            return instance;
        }
    }

    public IReadOnlyList<Worker> Workers => workers;

    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => instance = null;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    public int CountIn(BuildingService building)
    {
        int n = 0;
        foreach (var w in workers)
            if (w.building == building) n++;
        return n;
    }

    public bool CanAssign(Worker worker, BuildingService building) =>
        worker != null && building != null && worker.IsFree && CountIn(building) < MaxPerBuilding;

    public bool Assign(Worker worker, BuildingService building)
    {
        if (!CanAssign(worker, building)) return false;
        worker.building = building;
        building.SetWorkers(CountIn(building));
        Changed?.Invoke();
        return true;
    }

    public bool Release(Worker worker)
    {
        if (worker == null || worker.IsFree) return false;
        var building = worker.building;
        worker.building = null;
        if (building != null) building.SetWorkers(CountIn(building));
        Changed?.Invoke();
        return true;
    }

    public bool Toggle(Worker worker, BuildingService building)
    {
        if (worker == null) return false;
        return worker.building == building ? Release(worker) : Assign(worker, building);
    }
}
