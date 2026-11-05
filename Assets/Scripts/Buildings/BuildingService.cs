using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingService : MonoBehaviour
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
    [Range(0.05f, 1f)] [SerializeField] private float noWorkerSpeed = 1f / 3f;

    [Header("Pracovník")]
    [Tooltip("Rýchlosť podľa počtu pracovníkov: 1 = 1x, 2 = 1,5x, 3 = 2x (0 = noWorkerSpeed).")]
    [SerializeField] private float[] speedByWorkers = { 1f, 1.5f, 2f };
    [Tooltip("Voliteľné: objekt pracovníka. Skryje sa, keď budova pracovníka nemá.")]
    [SerializeField] private GameObject workerVisual;

    [Header("Ladenie")]
    [SerializeField] private bool logEvents;

    private WaveManager manager;
    private WaveSession session;
    private BuildingInfo info;
    private GuestPlaces places;
    private Seat[] seats = Array.Empty<Seat>();
    private readonly Queue<(WaveNeed need, float queuedAt)> queue = new();
    private int workers;

    public NeedType Need => info != null ? info.offeredNeed : NeedType.None;
    public int Seats => places != null ? places.Count : 0;
    public int QueueCount => queue.Count;
    public int Workers => workers;
    public bool HasWorker => workers > 0;
    public float Speed => workers <= 0 ? noWorkerSpeed
        : speedByWorkers[Mathf.Min(workers, speedByWorkers.Length) - 1];
    public Vector3 SeatPosition(int index) => places.GetPosition(index);

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
        workers = value;
        Changed?.Invoke();
    }

    private void Awake()
    {
        info = GetComponentInChildren<BuildingInfo>(true);
        places = GetComponentInChildren<GuestPlaces>(true);
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
    }

    private void OnDestroy()
    {
        if (session == null) return;
        session.NeedArrived -= OnNeedArrived;
        session.WaveStarted -= OnWaveStarted;
    }

    private void Update()
    {
        if (workerVisual != null && workerVisual.activeSelf != HasWorker)
            workerVisual.SetActive(HasWorker);

        EnsureSeats();
        float step = Time.deltaTime * Speed / serviceDuration;
        for (int i = 0; i < Seats; i++)
        {
            if (!seats[i].Occupied) continue;
            seats[i].Progress += step;
            if (seats[i].Progress >= 1f) Complete(i);
        }
        TrySeat();
    }

    private void OnNeedArrived(WaveNeed need)
    {
        if (need.Type != Need) return;
        queue.Enqueue((need, Time.time));
        Log($"{name}: need {need.Id} queued ({need.Type}), queue {queue.Count}");
        Changed?.Invoke();
    }

    private void OnWaveStarted(int wave)
    {
        queue.Clear();
        foreach (var s in seats) { s.Occupied = false; s.Progress = 0f; }
        Changed?.Invoke();
    }

    private void EnsureSeats()
    {
        if (seats.Length >= Seats) return;
        int old = seats.Length;
        Array.Resize(ref seats, Seats);
        for (int i = old; i < seats.Length; i++) seats[i] = new Seat();
    }

    private int FindFreeSeat()
    {
        for (int i = 0; i < Seats; i++)
            if (!seats[i].Occupied) return i;
        return -1;
    }

    private void TrySeat()
    {
        while (queue.Count > 0)
        {
            int free = FindFreeSeat();
            if (free < 0) return;
            var (need, queuedAt) = queue.Dequeue();
            if (!manager.BeginService(need.Id)) continue;

            seats[free].Occupied = true;
            seats[free].Need = need;
            seats[free].Progress = 0f;
            float waited = Time.time - queuedAt;
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
        if (logEvents) Debug.Log(message, this);
    }
}