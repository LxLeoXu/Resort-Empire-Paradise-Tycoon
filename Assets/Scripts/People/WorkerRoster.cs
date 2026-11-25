using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class WorkerRoster : MonoBehaviour, IGameSaveParticipant
{
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

    [Serializable]
    public sealed class SpecialistData
    {
        public VipRequiredRole role = VipRequiredRole.Butler;
        public string displayName = "Butler";
        [Min(0)] public int hireCost = 1000;
        [NonSerialized] public bool hired;
    }

    [Serializable]
    private sealed class SpecialistsSaveData
    {
        public List<int> hiredRoles = new();
    }

    [SerializeField] private List<Worker> workers = new()
    {
        new Worker("Chef Marco", 4),
        new Worker("Chef Lina", 2)
    };

    [Header("VIP Špecialisti (bez budovy, vo vlnách nerobia nič)")]
    [SerializeField] private List<SpecialistData> specialists = new()
    {
        new SpecialistData { role = VipRequiredRole.Butler, displayName = "Butler", hireCost = 1000 },
        new SpecialistData { role = VipRequiredRole.Captain, displayName = "Captain", hireCost = 1000 }
    };

    // Pripravené pre kolegov, keď neskôr pridajú najímanie manažérov.
    private readonly HashSet<VipRequiredRole> unlockedRoles = new();

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
    public IReadOnlyList<SpecialistData> Specialists => specialists;
    public string SaveKey => "specialists.v1";

    public event Action Changed;
    public event Action<Worker, BuildingService, BuildingService> WorkerMoved;
    public event Action SpecialistsChanged;

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
        EnsureDefaultSpecialists();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnValidate()
    {
        EnsureDefaultSpecialists();
    }

    private void EnsureDefaultSpecialists()
    {
        specialists ??= new List<SpecialistData>();
        if (!specialists.Exists(s => s != null && s.role == VipRequiredRole.Butler))
            specialists.Add(new SpecialistData { role = VipRequiredRole.Butler, displayName = "Butler", hireCost = 1000 });
        if (!specialists.Exists(s => s != null && s.role == VipRequiredRole.Captain))
            specialists.Add(new SpecialistData { role = VipRequiredRole.Captain, displayName = "Captain", hireCost = 1000 });
    }

    public static int GetMaxWorkersFor(BuildingService building) =>
        building != null ? building.MaxWorkers : 0;

    public int CountIn(BuildingService building)
    {
        int n = 0;
        foreach (var w in workers)
            if (w.building == building) n++;
        return n;
    }

    public bool CanAssign(Worker worker, BuildingService building) =>
        worker != null &&
        building != null &&
        worker.building != building &&
        CountIn(building) < GetMaxWorkersFor(building);

    public bool Assign(Worker worker, BuildingService building)
    {
        if (!CanAssign(worker, building)) return false;

        BuildingService previousBuilding = worker.building;

        worker.building = building;

        if (previousBuilding != null)
            previousBuilding.SetWorkers(CountIn(previousBuilding));

        building.SetWorkers(CountIn(building));

        Changed?.Invoke();

        if (previousBuilding != null)
            WorkerMoved?.Invoke(worker, previousBuilding, building);

        return true;
    }

    public bool Release(Worker worker)
    {
        if (worker == null || worker.IsFree) return false;

        var building = worker.building;
        worker.building = null;

        if (building != null)
            building.SetWorkers(CountIn(building));

        Changed?.Invoke();
        return true;
    }

    public bool Toggle(Worker worker, BuildingService building)
    {
        if (worker == null) return false;

        return worker.building == building
            ? Release(worker)
            : Assign(worker, building);
    }

    public bool RecruitAndAssign(BuildingService building)
    {
        if (building == null || CountIn(building) >= GetMaxWorkersFor(building))
            return false;

        Worker worker = new Worker("Chef " + (workers.Count + 1), 1);

        workers.Add(worker);
        worker.building = building;

        building.SetWorkers(CountIn(building));
        Changed?.Invoke();

        return true;
    }

    // =====================================================
    // VIP ŠPECIALISTI (BUTLER / CAPTAIN) & JEDNOTNÉ ROLE
    // =====================================================

    public SpecialistData GetSpecialist(VipRequiredRole role)
    {
        EnsureDefaultSpecialists();
        foreach (var s in specialists)
            if (s != null && s.role == role)
                return s;
        return null;
    }

    public bool IsSpecialistRole(VipRequiredRole role) =>
        role == VipRequiredRole.Butler || role == VipRequiredRole.Captain || GetSpecialist(role) != null;

    public bool HasSpecialist(VipRequiredRole role)
    {
        var s = GetSpecialist(role);
        return s != null && s.hired;
    }

    public int GetSpecialistHireCost(VipRequiredRole role)
    {
        var s = GetSpecialist(role);
        return s != null ? Mathf.Max(0, s.hireCost) : 0;
    }

    public string GetSpecialistName(VipRequiredRole role)
    {
        var s = GetSpecialist(role);
        return s != null && !string.IsNullOrWhiteSpace(s.displayName)
            ? s.displayName
            : GetRoleDisplayName(role);
    }

    public bool CanHireSpecialist(VipRequiredRole role)
    {
        var s = GetSpecialist(role);
        if (s == null || s.hired) return false;

        var waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager != null && waveManager.Session != null && waveManager.Session.State == WaveState.Running)
            return false;

        var resources = ResourceManager.Instance;
        return resources != null && resources.HasEnough(ResourceType.Coins, Mathf.Max(0, s.hireCost));
    }

    /// <summary>Hlavná funkcia na najatie špecialistu za mince (pre tlačidlo v S5).</summary>
    public bool TryHireSpecialist(VipRequiredRole role)
    {
        var s = GetSpecialist(role);
        if (s == null || s.hired) return false;

        var waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager != null && waveManager.Session != null && waveManager.Session.State == WaveState.Running)
            return false;

        var resources = ResourceManager.Instance;
        if (resources == null) return false;

        int cost = Mathf.Max(0, s.hireCost);
        if (cost > 0 && !resources.SpendResource(ResourceType.Coins, cost))
            return false;

        s.hired = true;
        GamePersistence.Instance?.SaveNow();

        SpecialistsChanged?.Invoke();
        Changed?.Invoke();

        Debug.Log($"<color=green>[SPECIALIST]</color> Najatý {s.displayName} za {cost} Coins.");
        return true;
    }

    /// <summary>Najatie špecialistu podľa ID/mena (napr. "Butler" alebo "Captain").</summary>
    public bool TryHireSpecialist(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return false;
        if (Enum.TryParse<VipRequiredRole>(roleName.Trim(), true, out var role) && IsSpecialistRole(role))
            return TryHireSpecialist(role);

        foreach (var s in specialists)
            if (s != null && string.Equals(s.displayName, roleName.Trim(), StringComparison.OrdinalIgnoreCase))
                return TryHireSpecialist(s.role);

        return false;
    }

    public bool TryHireButler() => TryHireSpecialist(VipRequiredRole.Butler);
    public bool TryHireCaptain() => TryHireSpecialist(VipRequiredRole.Captain);

    // Void verzie vhodné aj na priame napojenie v Unity Inspector Button.onClick
    public void HireButler() => TryHireButler();
    public void HireCaptain() => TryHireCaptain();

    /// <summary>Pre kolegov: odomkne/označí najatého manažéra alebo rolu.</summary>
    public void SetRoleUnlocked(VipRequiredRole role, bool unlocked)
    {
        if (role == VipRequiredRole.None) return;

        var spec = GetSpecialist(role);
        if (spec != null)
        {
            if (spec.hired == unlocked) return;
            spec.hired = unlocked;
        }
        else
        {
            bool changed = unlocked ? unlockedRoles.Add(role) : unlockedRoles.Remove(role);
            if (!changed) return;
        }

        SpecialistsChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Jeden jednotný spôsob kontroly pre špecialistu, manažéra aj Spa.</summary>
    public bool IsRoleAvailable(VipRequiredRole role)
    {
        switch (role)
        {
            case VipRequiredRole.None:
                return true;

            case VipRequiredRole.Butler:
            case VipRequiredRole.Captain:
                return HasSpecialist(role);

            case VipRequiredRole.Spa:
                return IsSpaBuilt();

            default:
                return unlockedRoles.Contains(role);
        }
    }

    public static bool IsSpaBuilt()
    {
        var infos = FindObjectsByType<BuildingInfo>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var info in infos)
        {
            if (info != null && info.IsSpa)
                return info.isBuilt && info.gameObject.activeInHierarchy;
        }

        var spots = FindObjectsByType<BuildingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var spot in spots)
        {
            if (spot != null && spot.gameObject.name.IndexOf("spa", StringComparison.OrdinalIgnoreCase) >= 0)
                return spot.isOccupied;
        }

        var shuttle = FindFirstObjectByType<SpaBoatShuttle>(FindObjectsInactive.Include);
        if (shuttle != null)
            return shuttle.gameObject.activeInHierarchy;

        return false;
    }

    public static string GetRoleDisplayName(VipRequiredRole role) => role switch
    {
        VipRequiredRole.Butler => "Butler",
        VipRequiredRole.Captain => "Captain",
        VipRequiredRole.Receptionist => "Receptionist",
        VipRequiredRole.Chef => "Chef",
        VipRequiredRole.Bartender => "Bartender",
        VipRequiredRole.Lifeguard => "Lifeguard",
        VipRequiredRole.Bellhop => "Bellhop",
        VipRequiredRole.Spa => "Spa",
        _ => string.Empty
    };

    public string CaptureSaveJson()
    {
        var save = new SpecialistsSaveData();
        EnsureDefaultSpecialists();
        foreach (var s in specialists)
            if (s != null && s.hired)
                save.hiredRoles.Add((int)s.role);
        return JsonUtility.ToJson(save);
    }

    public void RestoreSaveJson(string json)
    {
        EnsureDefaultSpecialists();
        foreach (var s in specialists)
            if (s != null) s.hired = false;

        if (!string.IsNullOrWhiteSpace(json))
        {
            var save = JsonUtility.FromJson<SpecialistsSaveData>(json);
            if (save != null && save.hiredRoles != null)
            {
                foreach (int roleValue in save.hiredRoles)
                {
                    var role = (VipRequiredRole)roleValue;
                    var spec = GetSpecialist(role);
                    if (spec != null) spec.hired = true;
                }
            }
        }

        SpecialistsChanged?.Invoke();
        Changed?.Invoke();
    }
}