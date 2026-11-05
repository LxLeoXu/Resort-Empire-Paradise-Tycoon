using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ModalWindow))]
public class UpgradeModalUI : MonoBehaviour
{
    [Header("BuildingCard")]
    [SerializeField] private Image buildingIcon;
    [SerializeField] private TextMeshProUGUI buildingTitle;
    [SerializeField] private TextMeshProUGUI buildingDescription;

    [Header("Capacity")]
    [SerializeField] private TextMeshProUGUI capacityLabel;
    [SerializeField] private TextMeshProUGUI capacityBefore;
    [SerializeField] private TextMeshProUGUI capacityAfter;

    [Header("Quality")]
    [SerializeField] private TextMeshProUGUI qualityLabel;
    [SerializeField] private TextMeshProUGUI qualityBefore;
    [SerializeField] private TextMeshProUGUI qualityAfter;

    [Header("Resources")]
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI resortValueText;

    [Header("Buttons")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button laterButton;

    private ModalWindow modalWindow;
    private BuildingInfo targetBuilding;

    private void Awake()
    {
        modalWindow = GetComponent<ModalWindow>();

        if (laterButton != null)
            laterButton.onClick.AddListener(OnLaterClicked);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
    }

    /// <summary>
    /// Otvorí S5 a naplní ho skutočnými informáciami z kliknutej budovy
    /// </summary>
    public void OpenForBuilding(BuildingInfo building)
    {
        if (building == null) return;

        targetBuilding = building;

        if (targetBuilding.currentLevel >= BuildingInfo.MaxLevel)
        {
            Debug.Log($"{building.buildingName} už je na maximálnej úrovni 3!");
            return;
        }

        // 1. Hlavička budovy
        string bName = string.IsNullOrEmpty(building.buildingName) ? building.gameObject.name : building.buildingName;
        if (buildingTitle != null) buildingTitle.text = bName;
        if (buildingIcon != null && building.buildingIcon != null) buildingIcon.sprite = building.buildingIcon;
        if (buildingDescription != null)
            buildingDescription.text = $"Level up to unlock more {building.offeredNeed.ToString().ToLower()} seats!";

        // 2. Kapacita (Ľavá strana = číslo, Šípka je v UI, Pravá strana = číslo)
        if (capacityLabel != null) capacityLabel.text = $"{bName} Seats";
        if (capacityBefore != null) capacityBefore.text = $"{building.CurrentCapacity}";
        if (capacityAfter != null) capacityAfter.text = $"{building.NextCapacity}";

        // 3. Kvalita (Ľavá strana = slovo, Šípka je v UI, Pravá strana = slovo)
        if (qualityLabel != null) qualityLabel.text = $"{building.offeredNeed} Quality";
        if (qualityBefore != null) qualityBefore.text = building.currentLevel == 1 ? "Basic" : "Improved";
        if (qualityAfter != null) qualityAfter.text = building.currentLevel == 1 ? "Improved" : "Premium";

        // 4. Cena a hodnota rezortu
        int cost = building.UpgradeCost;
        int gain = targetBuilding.ResortValueGain;

        if (costText != null) costText.text = $"{cost}";
        if (resortValueText != null) resortValueText.text = $"+{gain} Resort Value";

        // 5. Kontrola mincí cez HasEnough z ResourceManager
        bool hasEnoughCoins = true;
        if (ResourceManager.Instance != null)
        {
            hasEnoughCoins = ResourceManager.Instance.HasEnough(ResourceType.Coins, cost);
        }

        if (upgradeButton != null)
            upgradeButton.interactable = hasEnoughCoins;

        // Otvorenie modálneho okna
        if (modalWindow != null)
            modalWindow.Open();
        else
            gameObject.SetActive(true);

        Debug.Log($"<color=cyan>[S5 OKNO]</color> Vylepšenie pre: {bName}. Kapacita: {building.CurrentCapacity} → {building.NextCapacity}, Kvalita: {(building.currentLevel == 1 ? "Basic → Improved" : "Improved → Premium")}");
    }

    private void OnUpgradeClicked()
    {
        if (targetBuilding == null || !targetBuilding.CanUpgrade) return;

        int cost = targetBuilding.UpgradeCost;
        int gain = targetBuilding.ResortValueGain;

        // 1. Minutie mincí cez SpendResource a pridanie hodnoty rezortu cez AddResource
        if (ResourceManager.Instance != null)
        {
            if (!ResourceManager.Instance.SpendResource(ResourceType.Coins, cost))
            {
                Debug.LogWarning("Nemáš dosť mincí na tento upgrade!");
                return;
            }

            ResourceManager.Instance.AddResource(ResourceType.ResortValue, gain);
        }

        // 2. Budova sa hneď vylepší (bez čakania)
        targetBuilding.ApplyUpgrade();

        // 3. Zatvorenie okna S5
        if (modalWindow != null)
            modalWindow.Close();
        else
            gameObject.SetActive(false);
    }

    private void OnLaterClicked()
    {
        if (modalWindow != null)
            modalWindow.Close();
        else
            gameObject.SetActive(false);
    }
}