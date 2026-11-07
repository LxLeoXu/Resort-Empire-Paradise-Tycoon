using System.Collections.Generic;
using UnityEngine;

/// <summary>Boat arrival and guest visuals driven by the authoritative building queues.</summary>
[RequireComponent(typeof(WaveManager))]
public sealed class GuestWavePresentation : MonoBehaviour
{
    [SerializeField] private GameObject guestPrefab;
    [SerializeField] private GameObject boatPrefab;
    [SerializeField] private Transform boatApproach;
    [SerializeField] private Transform boatDock;
    [SerializeField] private Transform boatDepart;
    [SerializeField] private Transform pierEntrance;
    [SerializeField] private Transform guestExit;
    [SerializeField] private IslandGuestPaths islandPaths;
    [Min(0.1f)] [SerializeField] private float boatSpeed = 2f;

    private WaveSession session;
    private GameObject boat;
    private bool departing;
    private readonly Dictionary<int, GuestAgent> guests = new();
    private readonly List<BuildingService> services = new();
    private Transform visualRoot;

    public bool BoatDocked { get; private set; } = true;
    public int GuestCount => guests.Count;
    public bool TripInProgress => boat != null;
    public bool AllGuestsBoarded
    {
        get
        {
            foreach (var guest in guests.Values)
                if (guest == null || !guest.Boarded) return false;
            return true;
        }
    }
    public bool AllGuestsDisembarked
    {
        get
        {
            foreach (var guest in guests.Values)
                if (guest != null && !guest.Disembarked) return false;
            return true;
        }
    }

    public bool IsAtDestination(int id) => guests.TryGetValue(id, out var guest) && guest != null && guest.AtDestination;
    public bool IsAtQueuePosition(int id, Vector3 position) => guests.TryGetValue(id, out var guest) &&
        guest != null && guest.AtDestination && !guest.Boarded && Vector3.Distance(guest.transform.position, position) < 0.025f;

    public void JoinQueue(int id)
    {
        if (guests.TryGetValue(id, out var guest) && guest != null) guest.JoinQueue();
    }

    private void Start()
    {
        session = GetComponent<WaveManager>().Session;
        if (islandPaths == null) islandPaths = FindFirstObjectByType<IslandGuestPaths>();
        if (session == null || guestPrefab == null || boatPrefab == null || boatApproach == null ||
            boatDock == null || boatDepart == null || pierEntrance == null || guestExit == null)
        {
            Debug.LogError("GuestWavePresentation: assign guest/boat prefabs and the island's arrival anchors.", this);
            enabled = false;
            return;
        }
        visualRoot = new GameObject("WaveGuests").transform;
        // The manager lives on a UI Canvas. Guests must stay in world coordinates.
        visualRoot.SetParent(null);
        session.WaveStarted += OnWaveStarted;
        session.NeedArrived += OnNeedArrived;
    }

    private void OnWaveStarted(int wave)
    {
        foreach (Transform child in visualRoot) Destroy(child.gameObject);
        guests.Clear();
        session.SetGuestReturnPending(true);
        UnsubscribeServices();
        foreach (var service in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        {
            service.Seated += OnSeated;
            service.ServiceCompleted += OnServiceCompleted;
            services.Add(service);
        }
        if (boat != null) Destroy(boat);
        boat = Instantiate(boatPrefab, boatApproach.position, Quaternion.identity, visualRoot);
        departing = false;
        BoatDocked = false;
        session.HoldArrivalsOpen = true;
    }

    private void Update()
    {
        if (boat == null) return;
        if (!departing && BoatDocked && session.ArrivalsEnded && session.PendingCount == 0 && AllGuestsBoarded)
        {
            departing = true;
            BoatDocked = false;
            session.SetGuestReturnPending(false);
        }
        Vector3 target = departing ? boatDepart.position : boatDock.position;
        boat.transform.position = Vector3.MoveTowards(boat.transform.position, target, boatSpeed * Time.deltaTime);
        if (Vector3.Distance(boat.transform.position, target) > 0.01f) return;
        if (departing)
        {
            Destroy(boat);
            boat = null;
        }
        else BoatDocked = true;
    }

    private void LateUpdate()
    {
        foreach (var service in services)
        {
            if (service == null || service.Seats == 0) continue;
            int index = 0;
            foreach (var need in service.QueuedNeeds)
            {
                if (guests.TryGetValue(need.Id, out var guest) && guest != null)
                    guest.MoveTo(service.QueuePosition(index));
                index++;
            }
            foreach (var need in service.ApproachingNeeds)
            {
                if (guests.TryGetValue(need.Id, out var guest) && guest != null)
                    guest.MoveTo(service.QueuePosition(index));
                index++;
            }
        }
    }

    private void OnNeedArrived(WaveNeed need)
    {
        // Tick can emit the final need and try ending arrivals in the same frame.
        session.HoldArrivalsOpen = true;
        var instance = Instantiate(guestPrefab, boatDock.position, Quaternion.identity, visualRoot);
        instance.name = $"Guest_{need.Id}_{need.Type}";
        var guest = instance.GetComponent<GuestAgent>();
        guest.Disembark(guestExit.position, pierEntrance.position,
            islandPaths != null ? islandPaths.GetRoute(need.Type) : null);
        guests.Add(need.Id, guest);
    }

    private void OnSeated(WaveNeed need, int seat, float waited)
    {
        foreach (var service in services)
            if (service.Need == need.Type && guests.TryGetValue(need.Id, out var guest))
            {
                guest.BeginService(service.SeatPosition(seat), service.GetServiceApproach());
                break;
            }
    }

    private void OnServiceCompleted(WaveNeed need, int seat)
    {
        if (guests.TryGetValue(need.Id, out var guest))
            guest.ReturnToBoat(pierEntrance.position, guestExit.position, boatDock.position);
    }

    private void UnsubscribeServices()
    {
        foreach (var service in services)
        {
            if (service == null) continue;
            service.Seated -= OnSeated;
            service.ServiceCompleted -= OnServiceCompleted;
        }
        services.Clear();
    }

    private void OnDestroy()
    {
        UnsubscribeServices();
        if (session != null)
        {
            session.WaveStarted -= OnWaveStarted;
            session.NeedArrived -= OnNeedArrived;
        }
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }
}
