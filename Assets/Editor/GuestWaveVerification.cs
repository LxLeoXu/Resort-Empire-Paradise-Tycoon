using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Repeatable regression checks, callable from the menu or CLI script eval.</summary>
public static class GuestWaveVerification
{
    [MenuItem("Tools/Paradise Tycoon/Verify persistent wave queues")]
    public static void VerifySession()
    {
        var catalog = ScriptableObject.CreateInstance<WaveCatalog>();
        try
        {
            for (int wave = 0; wave < catalog.Count; wave++)
            {
                var session = new WaveSession(catalog);
                session.SetWaveIndex(wave);
                int ended = 0;
                session.WaveEnded += _ => ended++;
                session.NeedArrived += _ => session.HoldArrivalsOpen = true;
                Check(session.StartWave(1), "Wave must start.");
                session.Tick(1);
                Check(!session.ArrivalsEnded, "Arrival must wait for disembarkation.");
                session.HoldArrivalsOpen = false;
                session.Tick(0);
                int expected = catalog.GetWave(wave).TotalNeeds;
                Check(session.PendingCount == expected, "All needs must be queued.");
                session.Tick(86400); // An entire day without any service.
                Check(session.PendingCount == expected && session.State == WaveState.Running,
                    "Long waiting must never remove needs or end the wave.");
                session.SetWaveIndex(0);
                Check(!session.StartWave(1) && session.PendingCount == expected,
                    "Starting/resetting a running wave must not erase its queues.");
                foreach (var need in session.PendingNeeds.ToArray())
                {
                    Check(!session.ResolveNeed(need.Id), "Unserved needs cannot disappear.");
                    Check(session.BeginService(need.Id), "Queued need must enter service.");
                    Check(session.ResolveNeed(need.Id), "Served need must resolve.");
                    Check(!session.ResolveNeed(need.Id), "A need can only be served once.");
                }
                Check(session.State == WaveState.Result && session.PendingCount == 0 &&
                    session.ServedCount == expected && ended == 1, "Wave must end exactly once after every need is served.");
            }
            var empty = new WaveSession(catalog);
            empty.StartWave(1, Array.Empty<NeedType>());
            empty.Tick(1);
            Check(empty.State == WaveState.Result, "A wave with no built needs must still finish.");
            var returning = new WaveSession(catalog);
            returning.StartWave(1);
            returning.SetGuestReturnPending(true);
            returning.Tick(1);
            foreach (var need in returning.PendingNeeds.ToArray())
            {
                returning.BeginService(need.Id);
                returning.ResolveNeed(need.Id);
            }
            Check(returning.State == WaveState.Running && returning.PendingCount == 0,
                "Serving the last need must still wait for guests to board.");
            returning.SetGuestReturnPending(false);
            Check(returning.State == WaveState.Result, "Wave must finish once all served guests have boarded.");
            var travelling = new WaveSession(catalog);
            travelling.NeedArrived += need => Check(travelling.MarkInTransit(need.Id), "Incoming need must be tracked in transit.");
            travelling.StartWave(1);
            travelling.Tick(1);
            Check(travelling.QueuedCount == 0 && travelling.InTransitCount == travelling.PendingCount &&
                travelling.State == WaveState.Running, "Guests on their way must not count as queued or finish the wave.");
            travelling.Tick(86400);
            foreach (var need in travelling.PendingNeeds.ToArray())
            {
                Check(!travelling.BeginService(need.Id), "A travelling guest cannot start service.");
                Check(travelling.JoinQueue(need.Id) && !travelling.JoinQueue(need.Id), "Guest must join the physical queue exactly once.");
                Check(travelling.QueuedCount == 1 && travelling.GetQueueCount(need.Type) == 1,
                    "Only the guest who reached the building must be counted in its queue.");
                Check(travelling.BeginService(need.Id) && travelling.QueuedCount == 0 &&
                    travelling.GetQueueCount(need.Type) == 0, "Seating must remove the guest from waiting counts.");
                Check(travelling.ResolveNeed(need.Id), "A seated guest must still finish service.");
            }
            Check(travelling.State == WaveState.Result && travelling.InTransitCount == 0,
                "Wave must finish after every incoming guest has queued and been served.");
            Debug.Log("[GuestWaveVerification] PASS: persistent queues in all 8 waves, empty wave, return to boat, and separate travelling/queued/serving counts.");
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
