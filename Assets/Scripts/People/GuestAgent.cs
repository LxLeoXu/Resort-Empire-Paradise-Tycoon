using System.Collections.Generic;
using UnityEngine;

/// <summary>Autonomous visual for one queued need. No input handlers or colliders.</summary>
public sealed partial class GuestAgent : MonoBehaviour
{
    [Min(0.1f)] [SerializeField] private float speed = 1.8f;
    private readonly Queue<Vector3> waypoints = new();
    private Vector3 destination;
    private bool returningToBoat;
    private WalkCycle walk;
    private Vector3[] islandRoute = System.Array.Empty<Vector3>();
    private Vector3[] serviceApproach = System.Array.Empty<Vector3>();
    private int disembarkStepsRemaining;

    public bool Disembarked { get; private set; }
    public bool Boarded { get; private set; }
    public bool IsWaitingInQueue { get; private set; }
    public bool AtDestination => !returningToBoat && Disembarked && waypoints.Count == 0 &&
        Vector3.Distance(transform.position, destination) < 0.025f;

    private void Awake() => walk = GetComponent<WalkCycle>();

    public void Disembark(Vector3 landing, Vector3 entrance, Vector3[] route = null)
    {
        waypoints.Clear();
        Disembarked = false;
        Boarded = false;
        returningToBoat = false;
        IsWaitingInQueue = false;
        waypoints.Enqueue(landing);
        waypoints.Enqueue(entrance);
        disembarkStepsRemaining = 2;
        islandRoute = route ?? System.Array.Empty<Vector3>();
        serviceApproach = System.Array.Empty<Vector3>();
        foreach (var point in islandRoute) waypoints.Enqueue(point);
        destination = entrance;
    }

    public void MoveTo(Vector3 point)
    {
        if (!returningToBoat && !Boarded) destination = point;
    }

    public void JoinQueue() => IsWaitingInQueue = true;

    public void BeginService(Vector3 point, Vector3[] approach = null)
    {
        IsWaitingInQueue = false;
        serviceApproach = approach ?? System.Array.Empty<Vector3>();
        waypoints.Clear();
        foreach (var step in serviceApproach) waypoints.Enqueue(step);
        MoveTo(point);
    }

    public void ReturnToBoat(Vector3 entrance, Vector3 landing, Vector3 boardingPoint)
    {
        waypoints.Clear();
        for (int i = serviceApproach.Length - 1; i >= 0; i--) waypoints.Enqueue(serviceApproach[i]);
        // Retrace the same road back from the building before crossing the pier.
        for (int i = islandRoute.Length - 1; i >= 0; i--) waypoints.Enqueue(islandRoute[i]);
        waypoints.Enqueue(entrance);
        waypoints.Enqueue(landing);
        waypoints.Enqueue(boardingPoint);
        destination = boardingPoint;
        returningToBoat = true;
        IsWaitingInQueue = false;
    }

    private void Update()
    {
        if (Time.deltaTime <= 0 || GamePersistence.IsRestoring) return;
        Vector3 target = waypoints.Count > 0 ? waypoints.Peek() : destination;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if (Vector3.Distance(transform.position, target) > 0.01f) return;
        if (waypoints.Count > 0)
        {
            waypoints.Dequeue();
            if (!returningToBoat && !Disembarked && --disembarkStepsRemaining == 0)
                Disembarked = true;
            if (waypoints.Count == 0)
            {
                if (returningToBoat)
                {
                    Boarded = true;
                    gameObject.SetActive(false);
                }
            }
        }
        else if (walk != null) walk.Face(Vector2.up);
    }
}
