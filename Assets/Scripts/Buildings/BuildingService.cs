
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class BuildingService : MonoBehaviour
{
    private sealed class Seat
    {
        public bool Occupied;
        public WaveNeed Need;
        public float Progress;
    }

    [Header("Obsluha")]
    [Tooltip("Ako dlho trvá obsluha jedného hosťa s pracovníkom (sekundy). Číslo na ladenie.")]
    [Min(0.1f)] [SerializeField] private float serviceDuration = 4f;

    [Tooltip("Rýchlosť budovy bez pracovníka voči normálnej (asi tretina). Nikdy 0.")]
    [Range(0.05f, 1f)] [SerializeField] private float noWorkerSpeed = 0.33f;

    [Header("Pracovník")]
    [Tooltip("Rýchlosť podľa počtu pracovníkov: 1 = 1x, 2 = 1,5x, 3 = 2x (0 = noWorkerSpeed).")]
    [SerializeField] private float[] speedByWorkers = { 1f, 1.5f, 2f };
    [Tooltip("Voliteľné: objekt pracovníka. Skryje sa, keď budova pracovníka nemá.")]
    [SerializeField] private GameObject workerVisual;

    [Header("Zrýchlenie (energia)")]
    [Tooltip("Násobok rýchlosti počas zrýchlenia.")]
    [SerializeField] private float boostMultiplier = 2f;
    [Tooltip("Ako dlho zrýchlenie trvá (sekundy).")]
    [SerializeField] private float boostDuration = 15f;
    public const int BoostEnergyCost = 3;

    [Header("Prístup hostí")]
    [Tooltip("Body od radu po vstup do budovy, napríklad schody. Pri návrate ich hosť prejde opačne.")]
    [SerializeField] private Transform[] serviceApproach = Array.Empty<Transform>();
    [SerializeField] private Vector2 queueStep = new(-0.24f, -0.12f);
    [SerializeField] private Vector2 queueRowStep = new(0.15f, -0.28f);

    [Header("Ladenie")]
    [SerializeField] private bool logEvents;

    private WaveManager manager;
    private WaveSession session;
    private BuildingInfo info;
    private GuestPlaces places;
    private Seat[] seats = Array.Empty<Seat>();
    private readonly Queue<(WaveNeed need, float queuedAt)> queue = new();

    public float MaxWaitTime => queue.Count > 0 ? (Time.time - queue.Peek().queuedAt) : 0f;

    private readonly Queue<WaveNeed> approaching = new();
    private int workers;
    private SpeedBoostEffect speedBoost;
    private bool boostUsedThisWave;

    // RECOVERING stav teraz riadi WaveSession (podľa radu), nie pevný časovač.
    public bool IsRecovering => session != null &&
                                session.State == WaveState.Running &&
                                session.IsRecovering(Need);

    public NeedType Need => info != null ? info.offeredNeed : NeedType.None;
    public int Seats => places != null ? places.Count : 0;
    public int QueueCount => queue.Count;
    public int ApproachingCount => approaching.Count;
    public int Workers => workers;
    public bool HasWorker => workers > 0;
    public bool IsBoosted => speedBoost != null && speedBoost.IsPlaying;
    public bool BoostUsedThisWave => boostUsedThisWave;

    public bool CanBoost => speedBoost != null && info != null && info.isBuilt && !boostUsedThisWave
        && session != null && session.State == WaveState.Running;

    public bool TryBoost()
    {
        if (!CanBoost) return false;

        var resources = ResourceManager.Instance;
        if (resources == null || !resources.SpendResource(ResourceType.Energy, BoostEnergyCost)) return false;

        boostUsedThisWave = true;
        speedBoost.Play(boostDuration);
        Changed?.Invoke();
        return true;
    }

    public float Speed
    {
        get
        {
            float baseSpeed = workers <= 0 || speedByWorkers == null || speedByWorkers.Length == 0
                ? noWorkerSpeed : speedByWorkers[Mathf.Min(workers, speedByWorkers.Length) - 1];

            return Mathf.Max(0.05f, baseSpeed * (IsBoosted ? boostMultiplier : 1f));
        }
    }

    public Vector3 SeatPosition(int index) => places.GetPosition(index);

    public IEnumerable<WaveNeed> QueuedNeeds
    {
        get { foreach (var entry in queue) yield return entry.need; }
    }

    public IEnumerable<WaveNeed> ApproachingNeeds
    {
        get { foreach (var need in approaching) yield return need; }
    }

    public Vector3 QueuePosition(int index)
    {
        var anchor = transform.Find("GuestQueue");
        Vector3 front = anchor != null ? anchor.position : SeatPosition(0) + new Vector3(-0.3f, -0.35f, 0f);

        Vector2 offset = queueStep * (index % 6) + queueRowStep * (index / 6);
        return front + new Vector3(offset.x, offset.y, 0f);
    }

    public Vector3[] GetServiceApproach()
    {
        var points = new Vector3[serviceApproach.Length];

        for (int i = 0; i < points.Length; i++)
            points[i] = serviceApproach[i].position;

        return points;
    }

    public int OccupiedSeats
    {
        get
        {
            int n = 0;

            for (int i = 0; i < Mathf.Min(Seats, seats.Length); i++)
                if (seats[i].Occupied) n++;

            return n;
        }
    }

    public event Action<WaveNeed, int, float> Seated;
    public event Action<WaveNeed, int> ServiceCompleted;
    public event Action Changed;

    public void SetWorkers(int value)
    {
        value = Mathf.Clamp(value, 0, WorkerRoster.MaxPerBuilding);

        if (workers == value) return;

        int previous = workers;
        workers = value;

        // Pracovník pribudol počas vlny -> požiadame o RECOVERING.
        // Session ho zapne len ak je rad naozaj nad prahom (nie keď nie sú hostia).
        if (!GamePersistence.IsRestoring &&
            workers > previous &&
            session != null &&
            session.State == WaveState.Running)
        {
            session.RequestRecovery(Need);
        }

        Changed?.Invoke();
    }

    private void Awake()
    {
        info = GetComponentInChildren<BuildingInfo>(true);
        places = GetComponentInChildren<GuestPlaces>(true);
        speedBoost = GetComponent<SpeedBoostEffect>();

        if (info == null || places == null)
        {
            Debug.LogError("BuildingService: BuildingInfo or GuestPlaces missing on this building.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        manager = FindFirstObjectByType<WaveManager>();

        if (manager == null || manager.Session == null)
        {
            Debug.LogError("BuildingService: WaveManager not found.", this);
            enabled = false;
            return;
        }

        session = manager.Session;
        session.NeedArrived += OnNeedArrived;
        session.WaveStarted += OnWaveStarted;
        session.WaveEnded += OnWaveEnded;
    }

    private void OnDestroy()
    {
        if (session == null) return;

        session.NeedArrived -= OnNeedArrived;
        session.WaveStarted -= OnWaveStarted;
        session.WaveEnded -= OnWaveEnded;
    }

    private void Update()
    {
        if (workerVisual != null && workerVisual.activeSelf != HasWorker)
            workerVisual.SetActive(HasWorker);

        if (Time.deltaTime <= 0 || GamePersistence.IsRestoring || session == null)
            return;

        if (session.State != WaveState.Running)
            return;

        EnsureSeats();

        float step = Time.deltaTime * Speed / Mathf.Max(0.1f, serviceDuration);

        for (int i = 0; i < Seats; i++)
        {
            if (!seats[i].Occupied) continue;

            if (manager.Guests != null && manager.Guests.enabled &&
                !manager.Guests.IsAtDestination(seats[i].Need.Id))
                continue;

            seats[i].Progress += step;

            if (seats[i].Progress >= 1f)
                Complete(i);
        }

        AdmitApproachingGuests();
        TrySeat();
    }

    private void OnNeedArrived(WaveNeed need)
    {
        if (need.Type != Need || !info.isBuilt) return;

        if (manager.Guests != null && manager.Guests.enabled)
        {
            if (!session.MarkInTransit(need.Id)) return;

            approaching.Enqueue(need);
            return;
        }

        queue.Enqueue((need, Time.time));
        Log($"{name}: need {need.Id} queued ({need.Type}), queue {queue.Count}");
        Changed?.Invoke();
    }

    private void OnWaveStarted(int wave)
    {
        ClearRecovery();

        boostUsedThisWave = false;

        if (speedBoost != null)
            speedBoost.Stop();

        queue.Clear();
        approaching.Clear();

        foreach (var s in seats)
        {
            s.Occupied = false;
            s.Progress = 0f;
        }

        Changed?.Invoke();
    }

    private void OnWaveEnded(int wave)
    {
        ClearRecovery();
    }

    private void ClearRecovery()
    {
        if (session == null) return;

        session.ClearRecovery();
        Changed?.Invoke();
    }

    private void EnsureSeats()
    {
        if (seats.Length >= Seats) return;

        int old = seats.Length;
        Array.Resize(ref seats, Seats);

        for (int i = old; i < seats.Length; i++)
            seats[i] = new Seat();
    }

    private int FindFreeSeat()
    {
        for (int i = 0; i < Seats; i++)
            if (!seats[i].Occupied) return i;

        return -1;
    }

    private void AdmitApproachingGuests()
    {
        while (approaching.Count > 0)
        {
            var need = approaching.Peek();

            if (manager.Guests != null && manager.Guests.enabled &&
                !manager.Guests.IsAtQueuePosition(need.Id, QueuePosition(queue.Count)))
                return;

            if (!session.JoinQueue(need.Id)) return;

            approaching.Dequeue();
            queue.Enqueue((need, Time.time));

            if (manager.Guests != null && manager.Guests.enabled)
                manager.Guests.JoinQueue(need.Id);

            Changed?.Invoke();
        }
    }

    private void TrySeat()
    {
        while (queue.Count > 0)
        {
            int free = FindFreeSeat();
            if (free < 0) return;

            var (need, queuedAt) = queue.Peek();

            if (manager.Guests != null && manager.Guests.enabled &&
                !manager.Guests.IsAtDestination(need.Id))
                return;

            if (!manager.BeginService(need.Id)) return;

            queue.Dequeue();

            seats[free].Occupied = true;
            seats[free].Need = need;
            seats[free].Progress = 0f;

            float waited = Time.time - queuedAt;

            session.RecordQueueWait(need.Type, waited);

            Log($"{name}: need {need.Id} seated on {free + 1}, waited {waited:0.0}s");
            Seated?.Invoke(need, free, waited);
            Changed?.Invoke();
        }
    }

    private void Complete(int index)
    {
        WaveNeed need = seats[index].Need;

        seats[index].Occupied = false;
        seats[index].Progress = 0f;

        Log($"{name}: need {need.Id} served on {index + 1}");

        ServiceCompleted?.Invoke(need, index);
        manager.ResolveNeed(need.Id);

        Changed?.Invoke();
    }

    private void Log(string message)
    {
        if (logEvents)
            Debug.Log(message, this);
    }
}