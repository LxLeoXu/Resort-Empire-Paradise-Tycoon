using System;
using UnityEngine;

public class BuildingInfo : MonoBehaviour
{
    [Tooltip("Stable save identity. Keep unchanged when renaming or moving the building.")]
    [SerializeField] private string saveKey;
    public string SaveKey => saveKey;

    [Header("Building Information")]
    public string buildingName;
    public Sprite buildingIcon; // Ikona pre UI okno

    [Header("Building Status")]
    public bool isBuilt = true;

    [Header("Spa (bez vylepšení na Lv 2–5)")]
    [Tooltip("Zaškrtni pre Spa — nedá sa vylepšiť na úrovne 2–5 a nemá sloty pre pracovníkov.")]
    [SerializeField] private bool isSpa;

    [Header("Level & Capacity (Lv 1–5)")]
    [Range(1, MaxLevel)] public int currentLevel = 1;
    public const int MaxLevel = 5;

    [Header("Parametre úrovní 1–5 (Inspector)")]
    [Tooltip("Počet miest pre hostí na úrovniach 1 až 5 (3, 5, 7, 7, 10).")]
    [SerializeField] private int[] capacitiesByLevel = { 3, 5, 7, 7, 10 };

    [Tooltip("Počet slotov pre pracovníkov na úrovniach 1 až 5 (1, 1, 1, 2, 3).")]
    [SerializeField] private int[] workerSlotsByLevel = { 1, 1, 1, 2, 3 };

    [Tooltip("Cena vylepšenia na ďalšiu úroveň: [0]=Lv1->2 (500), [1]=Lv2->3 (1200), [2]=Lv3->4 (2500), [3]=Lv4->5 (5000).")]
    [SerializeField] private int[] upgradeCosts = { 500, 1200, 2500, 5000 };

    [Tooltip("Zisk hodnoty rezortu pri vylepšení: [0]=Lv1->2, [1]=Lv2->3, [2]=Lv3->4, [3]=Lv4->5.")]
    [SerializeField] private int[] resortValueGains = { 100, 100, 200, 300 };

    [Tooltip("Bonus kvality k spokojnosti hostí na úrovniach 1 až 5 (0, 0, 0, 0.10, 0.20).")]
    [SerializeField] private float[] qualityBonusByLevel = { 0f, 0f, 0f, 0.10f, 0.20f };

    [Header("Need")]
    public NeedType offeredNeed;

    public bool IsSpa => isSpa || HasSpaWord(buildingName) || HasSpaWord(saveKey);
    public int EffectiveMaxLevel => IsSpa ? 1 : MaxLevel;

    // Kapacita miest podľa Build dokumentu (3 -> 5 -> 7 -> 7 -> 10)
    public int CurrentCapacity => GetCapacity(currentLevel);

    public int NextCapacity => currentLevel < EffectiveMaxLevel
        ? GetCapacity(currentLevel + 1)
        : CurrentCapacity;

    // Sloty pre pracovníkov (1 -> 1 -> 1 -> 2 -> 3; Spa = 0)
    public int CurrentWorkerSlots => IsSpa ? 0 : GetWorkerSlots(currentLevel);

    public int NextWorkerSlots => IsSpa
        ? 0
        : (currentLevel < EffectiveMaxLevel ? GetWorkerSlots(currentLevel + 1) : CurrentWorkerSlots);

    // Cena upgradu (Lv1->2: 500, Lv2->3: 1200, Lv3->4: 2500, Lv4->5: 5000)
    public int UpgradeCost => CanUpgrade ? GetUpgradeCost(currentLevel) : 0;

    // Zisk hodnoty rezortu
    public int ResortValueGain => CanUpgrade ? GetResortValueGain(currentLevel) : 0;

    // Bonus kvality k spokojnosti (od Lv4)
    public float CurrentQualityBonus => IsSpa ? 0f : GetQualityBonus(currentLevel);

    public float NextQualityBonus => IsSpa
        ? 0f
        : (currentLevel < EffectiveMaxLevel ? GetQualityBonus(currentLevel + 1) : CurrentQualityBonus);

    public bool CanUpgrade => isBuilt && !IsSpa && currentLevel < EffectiveMaxLevel;

    public int GetCapacity(int level)
    {
        int clamped = Mathf.Clamp(level, 1, MaxLevel);
        int idx = clamped - 1;
        if (capacitiesByLevel != null && idx < capacitiesByLevel.Length && capacitiesByLevel[idx] > 0)
            return capacitiesByLevel[idx];
        return DefaultCapacityForLevel(clamped);
    }

    public int GetWorkerSlots(int level)
    {
        if (IsSpa) return 0;
        int clamped = Mathf.Clamp(level, 1, MaxLevel);
        int idx = clamped - 1;
        if (workerSlotsByLevel != null && idx < workerSlotsByLevel.Length && workerSlotsByLevel[idx] > 0)
            return workerSlotsByLevel[idx];
        return DefaultWorkerSlotsForLevel(clamped);
    }

    public int GetUpgradeCost(int fromLevel)
    {
        int clamped = Mathf.Clamp(fromLevel, 1, MaxLevel);
        int idx = clamped - 1;
        if (idx >= 0 && upgradeCosts != null && idx < upgradeCosts.Length && upgradeCosts[idx] >= 0)
            return upgradeCosts[idx];
        return DefaultUpgradeCostFromLevel(clamped);
    }

    public int GetResortValueGain(int fromLevel)
    {
        int clamped = Mathf.Clamp(fromLevel, 1, MaxLevel);
        int idx = clamped - 1;
        if (idx >= 0 && resortValueGains != null && idx < resortValueGains.Length && resortValueGains[idx] >= 0)
            return resortValueGains[idx];
        return DefaultResortValueGainFromLevel(clamped);
    }

    public float GetQualityBonus(int level)
    {
        if (IsSpa) return 0f;
        int clamped = Mathf.Clamp(level, 1, MaxLevel);
        int idx = clamped - 1;
        if (qualityBonusByLevel != null && idx < qualityBonusByLevel.Length && qualityBonusByLevel[idx] >= 0f)
            return qualityBonusByLevel[idx];
        return DefaultQualityBonusForLevel(clamped);
    }

    public static int DefaultCapacityForLevel(int level) => level switch
    {
        1 => 3,
        2 => 5,
        3 => 7,
        4 => 7,
        5 => 10,
        _ => 3
    };

    public static int DefaultWorkerSlotsForLevel(int level) => level switch
    {
        1 => 1,
        2 => 1,
        3 => 1,
        4 => 2,
        5 => 3,
        _ => 1
    };

    public static int DefaultUpgradeCostFromLevel(int fromLevel) => fromLevel switch
    {
        1 => 500,
        2 => 1200,
        3 => 2500,
        4 => 5000,
        _ => 0
    };

    public static int DefaultResortValueGainFromLevel(int fromLevel) => fromLevel switch
    {
        1 => 100,
        2 => 100,
        3 => 200,
        4 => 300,
        _ => 0
    };

    public static float DefaultQualityBonusForLevel(int level) => level switch
    {
        4 => 0.10f,
        5 => 0.20f,
        _ => 0f
    };

    public void ApplyUpgrade()
    {
        if (!CanUpgrade) return;

        currentLevel++;
        GetComponentInChildren<BuildingLevels>(true)?.RefreshAfterLoad();
        GetComponentInChildren<BuildingService>(true)?.NotifyBuildingUpgraded();
        Debug.Log($"<color=green>[UPGRADE]</color> {buildingName} bola vylepšená na Level {currentLevel}! Kapacita: {CurrentCapacity}, Sloty: {CurrentWorkerSlots}");
    }

    private void OnValidate()
    {
        EnsureIntArray(ref capacitiesByLevel, MaxLevel, DefaultCapacityForLevel);
        EnsureIntArray(ref workerSlotsByLevel, MaxLevel, DefaultWorkerSlotsForLevel);
        EnsureIntArray(ref upgradeCosts, MaxLevel - 1, DefaultUpgradeCostFromLevel);
        EnsureIntArray(ref resortValueGains, MaxLevel - 1, DefaultResortValueGainFromLevel);
        EnsureFloatArray(ref qualityBonusByLevel, MaxLevel, DefaultQualityBonusForLevel);
        currentLevel = Mathf.Clamp(currentLevel, 1, EffectiveMaxLevel);
    }

    private static void EnsureIntArray(ref int[] arr, int expectedLength, Func<int, int> defaultFor1BasedLevel)
    {
        if (arr != null && arr.Length == expectedLength) return;
        int oldLen = arr != null ? arr.Length : 0;
        Array.Resize(ref arr, expectedLength);
        for (int i = oldLen; i < expectedLength; i++)
            arr[i] = defaultFor1BasedLevel(i + 1);
    }

    private static void EnsureFloatArray(ref float[] arr, int expectedLength, Func<int, float> defaultFor1BasedLevel)
    {
        if (arr != null && arr.Length == expectedLength) return;
        int oldLen = arr != null ? arr.Length : 0;
        Array.Resize(ref arr, expectedLength);
        for (int i = oldLen; i < expectedLength; i++)
            arr[i] = defaultFor1BasedLevel(i + 1);
    }

    private static bool HasSpaWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string[] parts = value.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
            if (string.Equals(part, "spa", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void OnMouseDown()
    {
        // Ignorujeme kliknutie, ak hráč práve klikol na nejaké UI tlačidlo
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Ak budova stojí, otvoríme menu akcií
        if (isBuilt && BuildingActionMenuUI.Instance != null)
        {
            BuildingActionMenuUI.Instance.Open(this);
        }
    }
}