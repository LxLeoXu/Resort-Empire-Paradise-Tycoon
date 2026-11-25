using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// No GamePersistence instance and no file IO: the player's save is never touched.
public static class CoinsSpendingTests
{
    private static int passed;
    private static readonly List<GameObject> objects = new();
    private static readonly FieldInfo instance = typeof(ResourceManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);

    [MenuItem("Tools/Tests/Coins Spending S5 S9 HUD")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || ResourceManager.Instance != null || GamePersistence.Instance != null)
            throw new InvalidOperationException("Run in Edit Mode without a live game session.");
        passed = 0;
        try
        {
            foreach (int balance in new[] { 499, 500, 501, 5000 })
            {
                RunS5(balance);
                Cleanup();
                RunS9(balance);
                Cleanup();
            }
            RunInvalidS9();
            Cleanup();
            RunAffordability();
            Debug.Log("COINS TESTS PASS: " + passed + " checks. No player saves touched.");
        }
        finally { Cleanup(); }
    }

    private static GameObject New(string name)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        objects.Add(go);
        return go;
    }
    private static void Cleanup()
    {
        instance.SetValue(null, null);
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }
    private static ResourceManager Manager(int balance)
    {
        var manager = New("Test resources").AddComponent<ResourceManager>();
        instance.SetValue(null, manager);
        manager.RestoreResources(new List<ResourceSave> {
            new ResourceSave { type = ResourceType.Coins, amount = balance },
            new ResourceSave { type = ResourceType.Energy, amount = 45 },
            new ResourceSave { type = ResourceType.Materials, amount = 17 },
            new ResourceSave { type = ResourceType.ResortValue, amount = 700 }
        });
        return manager;
    }
    private static TMP_Text Hud(bool generic, ResourceType type = ResourceType.Coins)
    {
        var text = New("Test HUD").AddComponent<TextMeshProUGUI>();
        if (generic)
        {
            var counter = text.gameObject.AddComponent<ResourceCounter>();
            Set(counter, "resourceType", type); Set(counter, "valueText", text); Invoke(counter, "Start");
        }
        else
        {
            var counter = text.gameObject.AddComponent<CoinsCounter>();
            Set(counter, "coinsText", text); Invoke(counter, "Start");
        }
        return text;
    }
    private static void RunS5(int balance)
    {
        var manager = Manager(balance);
        var coins = Hud(false); var generic = Hud(true); var materials = Hud(true, ResourceType.Materials); var energy = Hud(true, ResourceType.Energy);
        var info = New("Test building").AddComponent<BuildingInfo>();
        var modal = New("Test S5").AddComponent<UpgradeModalUI>();
        modal.OpenForBuilding(info);
        int changes = 0;
        manager.OnResourceChanged += (type, amount) => { if (type == ResourceType.Coins) { changes++; Invoke(modal, "OnUpgradeClicked"); } };
        Invoke(modal, "OnUpgradeClicked");
        Invoke(modal, "OnUpgradeClicked");
        int expected = balance < 500 ? balance : balance - 500;
        Check("S5 balance " + balance, manager.GetResource(ResourceType.Coins) == expected);
        Check("S5 building state " + balance, info.currentLevel == (balance < 500 ? 1 : 2));
        Check("S5 single debit incl. reentry " + balance, changes == (balance < 500 ? 0 : 1));
        Check("S5 immediate HUD " + balance, coins.text == expected.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) && generic.text == coins.text);
        Check("S5 other currencies " + balance, materials.text == "17" && energy.text == "45/50" && manager.GetResource(ResourceType.ResortValue) == (balance < 500 ? 700 : 800));
    }
    private static BuildingSpot Spot()
    {
        var managerObject = New("Test wave manager"); managerObject.SetActive(false);
        var waveManager = managerObject.AddComponent<WaveManager>();
        var catalog = ScriptableObject.CreateInstance<WaveCatalog>();
        var session = new WaveSession(catalog);
        session.RestoreState(new WaveSave { number = 3, state = WaveState.Result });
        typeof(WaveManager).GetField("<Session>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(waveManager, session);
        UnityEngine.Object.DestroyImmediate(catalog);
        var spot = New("Test S9 spot").AddComponent<BuildingSpot>();
        Set(spot, "waveManager", waveManager); Set(spot, "unlockCost", 500); Set(spot, "requiredWave", 3);
        var prefab = New("Test building template"); prefab.AddComponent<BuildingInfo>();
        spot.buildingPrefabs.Add(prefab);
        return spot;
    }
    private static void RunS9(int balance)
    {
        var manager = Manager(balance); var coins = Hud(false); var generic = Hud(true);
        var spot = Spot();
        int changes = 0;
        manager.OnResourceChanged += (type, amount) => { if (type == ResourceType.Coins) { changes++; spot.BuildFirstBuilding(); } };
        Invoke(spot, "HandleBuyAndBuild");
        Invoke(spot, "HandleBuyAndBuild");
        if (spot.SpawnedBuilding != null) objects.Add(spot.SpawnedBuilding);
        int expected = balance < 500 ? balance : balance - 500;
        Check("S9 balance " + balance, manager.GetResource(ResourceType.Coins) == expected);
        Check("S9 building state " + balance, spot.isOccupied == (balance >= 500) && (spot.SpawnedBuilding != null) == (balance >= 500));
        Check("S9 single debit incl. reentry " + balance, changes == (balance < 500 ? 0 : 1));
        Check("S9 immediate HUD " + balance, coins.text == expected.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) && generic.text == coins.text);
        Check("S9 other currencies " + balance, manager.GetResource(ResourceType.Materials) == 17 && manager.GetResource(ResourceType.Energy) == 45 && manager.GetResource(ResourceType.ResortValue) == (balance < 500 ? 700 : 800));
    }
    private static void RunInvalidS9()
    {
        var manager = Manager(500); var spot = Spot();
        spot.BuildBuilding(-1);
        Check("S9 invalid index leaves coins/state intact", manager.GetResource(ResourceType.Coins) == 500 && !spot.isOccupied);
        Set(spot, "requiredWave", 4); spot.BuildFirstBuilding();
        Check("S9 locked wave cannot buy", manager.GetResource(ResourceType.Coins) == 500 && !spot.isOccupied);
        Set(spot, "requiredWave", 3); instance.SetValue(null, null); spot.BuildFirstBuilding();
        Check("S9 missing manager cannot build", !spot.isOccupied);
        instance.SetValue(null, manager);
        Check("ResourceManager rejects negative spend", !manager.SpendResource(ResourceType.Coins, -500) && manager.GetResource(ResourceType.Coins) == 500);
    }
    private static void RunAffordability()
    {
        var manager = Manager(499);
        var info = New("Affordability building").AddComponent<BuildingInfo>();
        var s5 = New("Affordability S5").AddComponent<UpgradeModalUI>();
        var missing = New("Missing coins").AddComponent<TextMeshProUGUI>();
        var button = New("Upgrade button").AddComponent<Button>();
        Set(s5, "missingCoinsText", missing); Set(s5, "upgradeButton", button);
        s5.OpenForBuilding(info); Invoke(s5, "OnEnable");
        Check("S5 missing coins text", missing.text == "Missing: 1 Coins" && !button.interactable);
        manager.AddResource(ResourceType.Coins, 1);
        Check("S5 affordability refresh", button.interactable && !missing.gameObject.activeSelf);
        Invoke(s5, "OnDisable");
        var s9 = New("Affordability S9").AddComponent<AreaUnlockUI>();
        var unlock = New("Unlock button").AddComponent<Button>();
        var label = New("Unlock label").AddComponent<TextMeshProUGUI>();
        Set(s9, "unlockButton", unlock); Set(s9, "unlockButtonText", label);
        int attempts = 0;
        s9.Setup("Test", null, "Relax", 500, false, true, 500, () => attempts++);
        Invoke(s9, "OnEnable");
        manager.SpendResource(ResourceType.Coins, 1);
        Check("S9 affordability refresh", !unlock.interactable);
        Invoke(s9, "OnButtonClicked"); Check("S9 disabled button does not invoke purchase", attempts == 0);
        manager.AddResource(ResourceType.Coins, 1); Invoke(s9, "OnButtonClicked"); Invoke(s9, "OnButtonClicked");
        Check("S9 failed action remains retryable", unlock.interactable && attempts == 2);
        Invoke(s9, "OnDisable");
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Check(string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException("COINS FAIL: " + name);
        passed++; Debug.Log("COINS PASS: " + name);
    }
}
