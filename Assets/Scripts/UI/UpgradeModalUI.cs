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

    [Header("S9 Area Unlock Reťazec")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private BuildingSpot poolSpot;
    [SerializeField] private BuildingSpot bungalowsSpot;

    private ModalWindow modalWindow;
    private BuildingInfo targetBuilding;
    private BuildingSpot spotToOpenAfterClose = null;

    private void Awake()
    {
        modalWindow = GetComponent<ModalWindow>();

        if (laterButton != null)
            laterButton.onClick.AddListener(OnLaterClicked);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);

        FindReferencesIfMissing();
    }

    private void FindReferencesIfMissing()
    {
        if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
        if (poolSpot == null || bungalowsSpot == null)
        {
            BuildingSpot[] spots = FindObjectsOfType<BuildingSpot>(true);
            foreach (var s in spots)
            {
                string sName = s.gameObject.name.ToLower();
                if (sName.Contains("pool")) poolSpot = s;
                else if (sName.Contains("bungalow")) bungalowsSpot = s;
            }
        }
    }

    /// <summary>
    /// Spustí sa VŽDY, keď sa S5 zatvorí (Upgrade, Later, alebo klik mimo okna)
    /// </summary>
    private void OnDisable()
    {
        if (spotToOpenAfterClose != null)
        {
            BuildingSpot target = spotToOpenAfterClose;
            spotToOpenAfterClose = null;

            Debug.Log($"<color=yellow>[S5 ZATVORENÉ]</color> Otváram S9 pre: {target.gameObject.name}");
            target.OpenS9();
        }
    }

    public void OpenRecommended()
    {
        FindReferencesIfMissing();

        // 1. Zistíme, či sme po 3. alebo 6. vlne a máme pripravené stavenisko
        CheckIfShouldQueueS9();

        // 2. Nájdeme budovu na upgrade
        BuildingInfo[] allBuildings = FindObjectsOfType<BuildingInfo>();
        BuildingInfo bestCandidate = null;

        foreach (var b in allBuildings)
        {
            if (b != null && b.isBuilt && b.CanUpgrade)
            {
                bestCandidate = b;
                break;
            }
        }

        if (bestCandidate != null)
        {
            OpenForBuilding(bestCandidate);
        }
        else
        {
            Debug.Log("Žiadna budova nepotrebuje upgrade, posúvam rovno na S9.");
            if (spotToOpenAfterClose != null)
            {
                BuildingSpot target = spotToOpenAfterClose;
                spotToOpenAfterClose = null;
                target.OpenS9();
            }
        }
    }

    private void CheckIfShouldQueueS9()
    {
        if (waveManager != null && waveManager.Session != null)
        {
            int currentWave = waveManager.Session.WaveNumber;

            if (currentWave == 3 && poolSpot != null && !poolSpot.isOccupied)
            {
                spotToOpenAfterClose = poolSpot;
            }
            else if (currentWave == 6 && bungalowsSpot != null && !bungalowsSpot.isOccupied)
            {
                spotToOpenAfterClose = bungalowsSpot;
            }
            else
            {
                spotToOpenAfterClose = null;
            }
        }
    }

    public void OpenForBuilding(BuildingInfo building)
    {
        if (building == null) return;
        targetBuilding = building;

        string bName = string.IsNullOrEmpty(building.buildingName) ? building.gameObject.name : building.buildingName;
        if (buildingTitle != null) buildingTitle.text = bName;
        if (buildingIcon != null && building.buildingIcon != null) buildingIcon.sprite = building.buildingIcon;
        if (buildingDescription != null)
            buildingDescription.text = $"Level up to unlock more {building.offeredNeed.ToString().ToLower()} seats!";

        if (capacityLabel != null) capacityLabel.text = $"{bName} Seats";
        if (capacityBefore != null) capacityBefore.text = $"{building.CurrentCapacity}";
        if (capacityAfter != null) capacityAfter.text = $"{building.NextCapacity}";

        if (qualityLabel != null) qualityLabel.text = $"{building.offeredNeed} Quality";
        if (qualityBefore != null) qualityBefore.text = building.currentLevel == 1 ? "Basic" : "Improved";
        if (qualityAfter != null) qualityAfter.text = building.currentLevel == 1 ? "Improved" : "Premium";

        int cost = building.UpgradeCost;
        int gain = targetBuilding.ResortValueGain;

        if (costText != null) costText.text = $"{cost}";
        if (resortValueText != null) resortValueText.text = $"+{gain} Resort Value";

        bool hasEnoughCoins = true;
        if (ResourceManager.Instance != null)
        {
            hasEnoughCoins = ResourceManager.Instance.HasEnough(ResourceType.Coins, cost);
        }

        if (upgradeButton != null)
            upgradeButton.interactable = hasEnoughCoins;

        if (modalWindow != null)
            modalWindow.Open();
        else
            gameObject.SetActive(true);
    }

    private void OnUpgradeClicked()
    {
        if (targetBuilding != null && targetBuilding.CanUpgrade)
        {
            int cost = targetBuilding.UpgradeCost;
            int gain = targetBuilding.ResortValueGain;

            if (ResourceManager.Instance != null)
            {
                if (!ResourceManager.Instance.SpendResource(ResourceType.Coins, cost))
                    return;

                ResourceManager.Instance.AddResource(ResourceType.ResortValue, gain);
            }

            targetBuilding.ApplyUpgrade();
        }

        CloseWindow();
    }

    private void OnLaterClicked()
    {
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (modalWindow != null)
            modalWindow.Close();
        else
            gameObject.SetActive(false);
    }
}