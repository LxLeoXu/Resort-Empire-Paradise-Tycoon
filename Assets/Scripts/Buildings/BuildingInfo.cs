using UnityEngine;

public class BuildingInfo : MonoBehaviour
{
    [Header("Building Information")]
    public string buildingName;
    public Sprite buildingIcon; // Ikona pre UI okno

    [Header("Building Status")]
    public bool isBuilt = true;
    public bool hasWorker = false;

    [Header("Level & Capacity (Prototype: Lv 1-3)")]
    [Range(1, 3)] public int currentLevel = 1;
    public const int MaxLevel = 3;

    [Header("Need")]
    public NeedType offeredNeed;

    // Kapacita miest podľa Build dokumentu (3 -> 5 -> 7)
    public int CurrentCapacity => currentLevel switch
    {
        1 => 3,
        2 => 5,
        3 => 7,
        _ => 3
    };

    public int NextCapacity => (currentLevel + 1) switch
    {
        2 => 5,
        3 => 7,
        _ => 7
    };

    // Cena upgradu podľa Build dokumentu (Lv1->2: 500, Lv2->3: 1200)
    public int UpgradeCost => currentLevel switch
    {
        1 => 500,
        2 => 1200,
        _ => 0
    };

    // Zisk hodnoty rezortu
    public int ResortValueGain => currentLevel switch
    {
        1 => 100,
        2 => 100,
        _ => 0
    };

    public bool CanUpgrade => isBuilt && currentLevel < MaxLevel;

    public void ApplyUpgrade()
    {
        if (!CanUpgrade) return;

        currentLevel++;
        Debug.Log($"<color=green>[UPGRADE]</color> {buildingName} bola vylepšená na Level {currentLevel}! Nová kapacita: {CurrentCapacity}");
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