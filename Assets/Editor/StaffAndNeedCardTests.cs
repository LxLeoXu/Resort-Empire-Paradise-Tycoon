using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class StaffAndNeedCardTests
{
    [MenuItem("Tools/Tests/Staff and S2 needs")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
        TestManagers();
        var catalog = ScriptableObject.CreateInstance<WaveCatalog>();
        try { TestWave(new WaveSession(catalog)); }
        finally { Object.DestroyImmediate(catalog); }
        TestCards();
        Debug.Log("STAFF AND S2 PASS");
    }

    private static void TestManagers()
    {
        var managers = Resources.Load<ManagerCatalog>("ManagerCatalog");
        var names = new[] { "Receptionist", "Chef", "Bartender", "Lifeguard", "Bellhop" };
        for (int i = 0; i < names.Length; i++)
        {
            var offer = managers.ForBuilding((NeedType)(i + 1));
            Check(offer?.displayName == names[i], names[i]);
            var english = LocalizationSettings.StringDatabase.GetTable("GameTexts");
            Check(english.GetEntry(offer.displayName) != null &&
                english.GetEntry($"building_{offer.buildingNeed}") != null, "Role and building keys");
            Check(offer.GetHireLabel(false).Contains(names[i]) && offer.GetHireLabel(false).Contains(offer.coinPrice.ToString()) &&
                offer.GetHireLabel(true).Contains(names[i]) && !offer.GetHireLabel(true).Contains("{"), "Localized manager labels");
        }
        Check(managers.ForBuilding(NeedType.None) == null, "No offer for Spa");

        var staffPanel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/S3_AssignStaff.prefab");
        var bindings = new SerializedObject(staffPanel.GetComponent<WorkerAssignUI>());
        Check(bindings.FindProperty("hireManagerButton").objectReferenceValue is Button &&
            bindings.FindProperty("managerLabel").objectReferenceValue is TMP_Text, "Hire controls bound");

        var building = new GameObject("Test reception");
        var wallet = new GameObject("Test wallet");
        var singleton = typeof(ResourceManager).GetProperty(nameof(ResourceManager.Instance));
        var originalWallet = ResourceManager.Instance;
        try
        {
            var resources = wallet.AddComponent<ResourceManager>();
            singleton.SetValue(null, resources);
            building.AddComponent<BuildingInfo>().offeredNeed = NeedType.Rooms;
            building.AddComponent<GuestPlaces>();
            var service = building.AddComponent<BuildingService>();
            typeof(BuildingService).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(service, null);
            float unattended = service.Speed;
            service.SetWorkers(1);
            float staffed = service.Speed;
            var offer = service.ManagerOffer;
            Check(!service.TryHireManager() && !service.HasManager, "Cannot hire without coins");
            resources.AddResource(ResourceType.Coins, offer.coinPrice * 2);
            Check(service.TryHireManager() && service.HasManager, "Hired for this building");
            Check(!service.TryHireManager() && resources.GetResource(ResourceType.Coins) == offer.coinPrice,
                "Cannot charge twice");
            Check(Mathf.Approximately(service.Speed, staffed * offer.speedMultiplier), "Staffed speed");
            service.SetWorkers(0);
            Check(Mathf.Approximately(service.Speed, unattended * offer.speedMultiplier), "Unattended speed");
            var saved = JsonUtility.FromJson<BuildingSave>(JsonUtility.ToJson(service.CaptureState("reception")));
            Check(saved.hasManager && !JsonUtility.FromJson<BuildingSave>("{}").hasManager, "Manager save and legacy default");
        }
        finally
        {
            singleton.SetValue(null, originalWallet);
            Object.DestroyImmediate(wallet);
            Object.DestroyImmediate(building);
        }
    }

    private static void TestWave(WaveSession wave)
    {
        wave.StartWave(1, null, new WaveDefinition(2, 1, 0, 1, 0, 0));
        Check(wave.GetCardStatus(NeedType.Food, true, false) == NeedCardStatus.Ok, "Waiting for arrivals");
        wave.Tick(1);
        var food = wave.PendingNeeds.Single(need => need.Type == NeedType.Food);
        wave.UpdateBottleneck(food.Type, 5, 1, 1, 3, 0);
        Check(wave.GetCardStatus(food.Type, false, true) == NeedCardStatus.NoWorker, "No worker takes precedence over full");
        Check(wave.GetCardStatus(food.Type, true, true) == NeedCardStatus.Full, "Staffed capacity warning");
        Check(wave.GetCardStatus(food.Type, true, false) == NeedCardStatus.Bottleneck, "Free seat suppresses full");
        wave.RequestRecovery(food.Type);
        wave.UpdateBottleneck(food.Type, 5, 1, 1, 3, 0);
        Check(wave.GetCardStatus(food.Type, true, true) == NeedCardStatus.Recovering, "Recovery distinct from full");
        wave.UpdateBottleneck(NeedType.Rooms, 8, 2, 1, 3, 0);
        Check(wave.GetMostDelayedNeed(3, true) == NeedType.Rooms, "Longest live queue wins");
        wave.UpdateBottleneck(NeedType.Rooms, 0, 0, 1, 3, 0);
        Check(wave.GetMostDelayedNeed(3, true) == food.Type, "Complaint switches as queue drains");
        wave.UpdateBottleneck(food.Type, 0, 0, 1, 3, 0);
        wave.RecordQueueWait(food.Type, 5);
        Check(wave.GetMostDelayedNeed(3, true) == NeedType.None, "Historic wait does not keep complaint visible");
        wave.MarkInTransit(food.Id);
        Check(wave.GetCardStatus(food.Type, true, false) == NeedCardStatus.Ok, "Approaching guest still pending");
        wave.JoinQueue(food.Id);
        wave.BeginService(food.Id);
        Check(wave.GetCardStatus(food.Type, true, false) == NeedCardStatus.Ok, "Seated guest still pending");
        wave.ResolveNeed(food.Id);
        Check(wave.GetCardStatus(food.Type, true, false) == NeedCardStatus.Satisfied, "Completed need during wave tail");
    }

    private static void TestCards()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Panels/S2_WaveNeedsPanel.prefab");
        var panel = Object.Instantiate(prefab);
        try
        {
            var controller = panel.GetComponent<WaveNeedsPanel>();
            const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(WaveNeedsPanel).GetMethod("Awake", instance).Invoke(controller, null);
            var setComplaint = typeof(WaveNeedsPanel).GetMethod("SetComplaint", instance);
            var complaint = panel.transform.Find("ScrollArea/Rows/Complaint/Text").GetComponent<TMP_Text>();
            Check(!complaint.transform.parent.gameObject.activeSelf, "Smooth wave starts without a complaint");
            var cards = panel.GetComponentsInChildren<NeedCardUI>(true);
            Check(cards.Length == 5, "All five cards");
            var table = LocalizationSettings.StringDatabase.GetTable("GameTexts");
            foreach (var card in cards)
            {
                setComplaint.Invoke(controller, new object[] { card.needType });
                var entry = table.GetEntry("s2_complaint_" + card.needType);
                Check(entry != null && complaint.text == entry.LocalizedValue &&
                    complaint.transform.parent.gameObject.activeSelf, "Complaint for " + card.needType);
                var warning = card.transform.Find("WarningSection/Text").GetComponent<TMP_Text>();
                var recovery = card.transform.Find("RecoveringSection/Text").GetComponent<TMP_Text>();
                card.ShowStatus(NeedCardStatus.NoWorker, "Building");
                Color noWorker = warning.color;
                card.ShowStatus(NeedCardStatus.Full, "Building");
                Check(warning.color != noWorker && warning.text == table.GetEntry("s2_full").LocalizedValue, "Full badge");
                card.ShowStatus(NeedCardStatus.Recovering, "Building");
                Check(recovery.text.Contains("Building") && !recovery.text.Contains("{"), "Recovery includes building name");
                Color recovering = recovery.color;
                card.ShowStatus(NeedCardStatus.Satisfied, "Building");
                Check(recovery.color != recovering && recovery.text == "SATISFIED" &&
                    !warning.transform.parent.gameObject.activeSelf, "Satisfied badge");
                card.ShowStatus(NeedCardStatus.Ok, "Building");
                Check(!recovery.transform.parent.gameObject.activeSelf, "Clear on next wave");
            }
            setComplaint.Invoke(controller, new object[] { NeedType.None });
            Check(!complaint.transform.parent.gameObject.activeSelf, "Hide complaint when the queue clears");
        }
        finally { Object.DestroyImmediate(panel); }
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("STAFF AND S2 FAIL: " + label);
    }
}
