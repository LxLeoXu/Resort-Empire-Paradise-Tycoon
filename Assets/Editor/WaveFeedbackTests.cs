using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Synthetic wave data only. No scene changes and no access to player save files.
public static class WaveFeedbackTests
{
    private static int passed;
#if UNITY_EDITOR
    [MenuItem("Tools/Tests/Wave Feedback")]
#endif
    public static void Run()
    {
        passed = 0;
        var catalog = ScriptableObject.CreateInstance<WaveCatalog>();
        try
        {
            var wave = new WaveSession(catalog);
            var data = Snapshot(0, 2, 0, 2);
            wave.RestoreState(data);
            Check("No served guests means no praise", wave.GetBestServedNeedType() == NeedType.None);
            data = Snapshot(2, 2, 3, 4); wave.RestoreState(data);
            Check("Success ratio beats raw count", wave.GetBestServedNeedType() == NeedType.Rooms);
            data = Snapshot(1, 2, 2, 4); wave.RestoreState(data);
            Check("Equal ratio prefers more served", wave.GetBestServedNeedType() == NeedType.Food);
            data = Snapshot(2, 2, 2, 2); wave.RestoreState(data);
            Check("Full tie uses stable Rooms first", wave.GetBestServedNeedType() == NeedType.Rooms);
            data = Snapshot(0, 0, 1, 1); wave.RestoreState(data);
            Check("Absent need ignored", wave.GetBestServedNeedType() == NeedType.Food);
            Check("No measured delay", wave.GetMostDelayedNeed(8) == NeedType.None);
            wave.RecordQueueWait(NeedType.Rooms, 7);
            Check("Below threshold is smooth", wave.GetMostDelayedNeed(8) == NeedType.None);
            wave.RecordQueueWait(NeedType.Food, 8);
            Check("Threshold inclusive", wave.GetMostDelayedNeed(8) == NeedType.Food);
            wave.RecordQueueWait(NeedType.Rooms, 8);
            Check("Delay tie stable", wave.GetMostDelayedNeed(8) == NeedType.Rooms);
            wave.RecordQueueWait(NeedType.Food, 12);
            Check("Worst measured delay wins", wave.GetMostDelayedNeed(8) == NeedType.Food);
            Check("Average samples", wave.GetAverageQueueWait(NeedType.Food) == 10 && wave.GetQueueWaitSampleCount(NeedType.Food) == 2);
            wave.RecordQueueWait(NeedType.Food, float.NaN); wave.RecordQueueWait(NeedType.Food, float.PositiveInfinity);
            wave.RecordQueueWait(NeedType.Food, -1); wave.RecordQueueWait((NeedType)99, 100);
            Check("Invalid measurements ignored", wave.GetQueueWaitSampleCount(NeedType.Food) == 2 && wave.GetLongestQueueWait((NeedType)99) == 0);
            var captured = wave.CaptureState();
            var restored = new WaveSession(catalog); restored.RestoreState(captured);
            Check("Capture restore preserves delays", restored.GetMostDelayedNeed(8) == NeedType.Food && restored.GetAverageQueueWait(NeedType.Food) == 10);
            Check("Capture restore preserves positive", restored.GetBestServedNeedType() == NeedType.Food);
            captured.queueWaits = null; restored.RestoreState(captured);
            Check("Legacy absent waits accepted", restored.GetMostDelayedNeed(8) == NeedType.None);
            captured.state = WaveState.Result; restored.RestoreState(captured);
            Check("Next wave starts", restored.StartWave(10, null, new WaveDefinition(1, 1, 0, 0, 0, 0)));
            Check("New wave resets feedback", restored.GetBestServedNeedType() == NeedType.None && restored.CaptureQueueWaitStatistics().Count == 0);
            restored.Tick(10); foreach (var need in new List<WaveNeed>(restored.PendingNeeds)) { restored.BeginService(need.Id); restored.ResolveNeed(need.Id); }
            Check("Second wave has its own positive", restored.GetBestServedNeedType() == NeedType.Rooms);
            Check("Second wave smooth", restored.GetMostDelayedNeed(8) == NeedType.None);
            var valid = new QueueWaitSave { type = NeedType.Food, count = 2, longestWait = 12, totalWait = 20 };
            Check("Valid statistics accepted", QueueWaitSave.AreValid(new List<QueueWaitSave> { valid }));
            Check("Duplicates rejected", !QueueWaitSave.AreValid(new List<QueueWaitSave> { valid, valid }));
            valid.totalWait = 1; Check("Inconsistent total rejected", !QueueWaitSave.AreValid(new List<QueueWaitSave> { valid }));
            valid.totalWait = float.NaN; Check("NaN rejected", !QueueWaitSave.AreValid(new List<QueueWaitSave> { valid }));
            valid.totalWait = 20; valid.type = (NeedType)99; Check("Unknown need rejected", !QueueWaitSave.AreValid(new List<QueueWaitSave> { valid }));
            valid.type = NeedType.Food; valid.count = 0; Check("Invalid count rejected", !QueueWaitSave.AreValid(new List<QueueWaitSave> { valid }));
            Check("Legacy null accepted", QueueWaitSave.AreValid(null));
#if UNITY_EDITOR
            var table = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTable("GameTexts");
            foreach (string key in new[] { "WaveFeedback_Positive", "WaveFeedback_Improvement", "WaveFeedback_Smooth", "WaveFeedback_NoData" })
                Check("GameTexts entry " + key, table != null && table.GetEntry(key) != null);
            var roundTrip = JsonUtility.FromJson<WaveSave>(JsonUtility.ToJson(wave.CaptureState()));
            restored.RestoreState(roundTrip);
            Check("Unity JSON retains feedback", restored.GetMostDelayedNeed(8) == NeedType.Food && restored.GetBestServedNeedType() == NeedType.Food);
            WaveS4Connector.BuildFeedback(restored, 8, out string positive, out string negative);
            Check("Localized argument positive", positive == "Food went great!");
            Check("Localized argument improvement", negative == "Restaurant capacity needs improvement");
            restored.RestoreState(new WaveSave());
            WaveS4Connector.BuildFeedback(restored, 8, out positive, out negative);
            Check("Localized no data", positive == "Not enough data yet");
            Check("Localized smooth", negative == "Everything ran smoothly");
#endif
            Debug.Log("WAVE FEEDBACK PASS: " + passed + " checks");
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
    }
    private static WaveSave Snapshot(int roomsServed, int rooms, int foodServed, int food)
    {
        var data = new WaveSave { number = 1, state = WaveState.Running, emitted = rooms + food, served = roomsServed + foodServed };
        for (int i = 0; i < rooms; i++) data.arrivals.Add(NeedType.Rooms);
        for (int i = 0; i < food; i++) data.arrivals.Add(NeedType.Food);
        data.servedByType.Add(new CountSave { type = NeedType.Rooms, count = roomsServed });
        data.servedByType.Add(new CountSave { type = NeedType.Food, count = foodServed });
        return data;
    }
    private static void Check(string label, bool condition)
    {
        if (!condition) throw new InvalidOperationException("WAVE FEEDBACK FAIL: " + label);
        passed++;
    }
}
