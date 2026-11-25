using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

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

    [Header("Worker Slots (Optional)")]
    [SerializeField] private TextMeshProUGUI slotsLabel;
    [SerializeField] private TextMeshProUGUI slotsBefore;
    [SerializeField] private TextMeshProUGUI slotsAfter;

    [Header("Quality")]
    [SerializeField] private TextMeshProUGUI qualityLabel;
    [SerializeField] private TextMeshProUGUI qualityBefore;
    [SerializeField] private TextMeshProUGUI qualityAfter;

    [Header("Resources")]
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private GameObject materialCostGroup;
    [SerializeField] private TextMeshProUGUI materialCostText;
    [SerializeField] private TextMeshProUGUI resortValueText;
    [SerializeField] private TextMeshProUGUI missingCoinsText;
    [SerializeField] private TextMeshProUGUI missingMaterialsText;

    [Header("VIP Špecialisti v S5 (Voliteľné tlačidlá)")]
    [SerializeField] private Button hireButlerButton;
    [SerializeField] private TextMeshProUGUI hireButlerLabel;
    [SerializeField] private TextMeshProUGUI hireButlerCostText;
    [SerializeField] private Button hireCaptainButton;
    [SerializeField] private TextMeshProUGUI hireCaptainLabel;
    [SerializeField] private TextMeshProUGUI hireCaptainCostText;

    [Header("Buttons")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button laterButton;

    [Header("Recommendation presentation")]
    [SerializeField] private TextMeshProUGUI windowTitle;
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private GameObject[] upgradeOnlyContent;
    [SerializeField] private GameObject noUpgradeContent;
    [SerializeField] private TextMeshProUGUI laterButtonLabel;
    private bool presentationCaptured;
    private TMP_Text messageLabel, upgradeButtonLabel;
    private Vector2 originalWindowSize, originalHeaderSize, originalButtonMin, originalButtonMax, originalButtonPosition, originalButtonSize, originalButtonPivot;
    private bool[] originalContentVisible;
    private Vector3 originalButtonRowScale;
    private float originalLaterFontSize;

    [Header("S9 Area Unlock Reťazec")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private BuildingSpot poolSpot;
    [SerializeField] private BuildingSpot bungalowsSpot;

    private bool purchaseInProgress, purchaseCompleted;
    private ResourceManager observedResources;
    private WorkerRoster observedRoster;
    private ModalWindow modalWindow;
    private BuildingInfo targetBuilding;
    public BuildingInfo TargetBuilding => targetBuilding;
    private BuildingSpot spotToOpenAfterClose = null;

    private void Awake()
    {
        modalWindow = GetComponent<ModalWindow>();
        CapturePresentation();

        if (laterButton != null)
            laterButton.onClick.AddListener(OnLaterClicked);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);

        if (hireButlerButton != null)
            hireButlerButton.onClick.AddListener(OnHireButlerClicked);

        if (hireCaptainButton != null)
            hireCaptainButton.onClick.AddListener(OnHireCaptainClicked);

        FindReferencesIfMissing();
    }

    private void CapturePresentation()
    {
        if (presentationCaptured) return;
        presentationCaptured = true;
        if (noUpgradeContent != null) messageLabel = noUpgradeContent.GetComponent<TMP_Text>();
        if (upgradeButton != null) upgradeButtonLabel = upgradeButton.GetComponentInChildren<TMP_Text>(true);
        if (windowTitle != null) originalHeaderSize = windowTitle.rectTransform.sizeDelta;
        if (laterButtonLabel != null) originalLaterFontSize = laterButtonLabel.fontSize;
        if (windowRect != null) originalWindowSize = windowRect.sizeDelta;
        originalContentVisible = new bool[upgradeOnlyContent != null ? upgradeOnlyContent.Length : 0];
        for (int i = 0; i < originalContentVisible.Length; i++)
            originalContentVisible[i] = upgradeOnlyContent[i] != null && upgradeOnlyContent[i].activeSelf;
        if (laterButton == null) return;
        var rect = (RectTransform)laterButton.transform;
        originalButtonRowScale = rect.parent.localScale;
        originalButtonMin = rect.anchorMin; originalButtonMax = rect.anchorMax; originalButtonPivot = rect.pivot;
        originalButtonPosition = rect.anchoredPosition; originalButtonSize = rect.sizeDelta;
    }

    private void SetRecommendationPresentation(bool hasRecommendation)
    {
        CapturePresentation();
        if (windowTitle != null) windowTitle.rectTransform.sizeDelta = hasRecommendation ? originalHeaderSize :
            new Vector2(Mathf.Max(originalHeaderSize.x, originalWindowSize.x - 100), originalHeaderSize.y);
        if (windowRect != null) windowRect.sizeDelta = hasRecommendation ? originalWindowSize : new Vector2(originalWindowSize.x, 320f);
        for (int i = 0; i < originalContentVisible.Length; i++)
            if (upgradeOnlyContent[i] != null) upgradeOnlyContent[i].SetActive(hasRecommendation && originalContentVisible[i]);
        if (noUpgradeContent != null) noUpgradeContent.SetActive(!hasRecommendation);
        if (laterButtonLabel != null) laterButtonLabel.fontSize = hasRecommendation ? originalLaterFontSize : 36;
        if (laterButton == null) return;
        var rect = (RectTransform)laterButton.transform;
        rect.parent.localScale = hasRecommendation ? originalButtonRowScale : Vector3.one;
        rect.anchorMin = hasRecommendation ? originalButtonMin : new Vector2(.5f, .5f);
        rect.anchorMax = hasRecommendation ? originalButtonMax : new Vector2(.5f, .5f);
        rect.pivot = hasRecommendation ? originalButtonPivot : new Vector2(.5f, .5f);
        rect.anchoredPosition = hasRecommendation ? originalButtonPosition : Vector2.zero;
        rect.sizeDelta = hasRecommendation ? originalButtonSize : new Vector2(240, 70);
        if (!hasRecommendation && windowRect != null)
            rect.position = windowRect.TransformPoint(new Vector3(0, windowRect.rect.yMin + 65, 0));
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
    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        observedResources = ResourceManager.Instance;
        if (observedResources != null) observedResources.OnResourceChanged += OnResourceChanged;
        observedRoster = WorkerRoster.Instance;
        if (observedRoster != null) observedRoster.SpecialistsChanged += RefreshSpecialistButtons;
        RefreshAffordability();
        RefreshSpecialistButtons();
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        if (type == ResourceType.Coins || type == ResourceType.Materials)
        {
            RefreshAffordability();
            if (type == ResourceType.Coins) RefreshSpecialistButtons();
        }
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (observedResources != null) observedResources.OnResourceChanged -= OnResourceChanged;
        observedResources = null;
        if (observedRoster != null) observedRoster.SpecialistsChanged -= RefreshSpecialistButtons;
        observedRoster = null;
        targetBuilding = null;
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

        // SCRUM-38: only a measured bottleneck may create a recommendation.
        BuildingInfo[] allBuildings = FindObjectsOfType<BuildingInfo>();
        BuildingInfo bestCandidate = null;
        NeedType recommended = waveManager != null && waveManager.Session != null ? waveManager.Session.GetRecommendedNeed() : NeedType.None;

        foreach (var b in allBuildings)
        {
            if (b != null && b.isBuilt && b.offeredNeed == recommended && recommended != NeedType.None)
            {
                bestCandidate = b;
                break;
            }
        }

        OpenForBuilding(bestCandidate);
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
        if (purchaseInProgress) return;
        purchaseCompleted = false;
        targetBuilding = building;
        SetRecommendationPresentation(building != null && building.CanUpgrade);
        RefreshContent();
        if (modalWindow != null) modalWindow.Open(); else gameObject.SetActive(true);
    }

    private static string Text(string key, params object[] args) =>
        LocalizationSettings.StringDatabase.GetLocalizedString("GameTexts", key, args);

    private static string TextWithFallback(string key, string fallbackFormat, params object[] args)
    {
        try
        {
            string value = LocalizationSettings.StringDatabase.GetLocalizedString("GameTexts", key, args);
            if (!string.IsNullOrWhiteSpace(value) &&
                !value.StartsWith("No translation found", System.StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }
        catch { }

        return string.Format(fallbackFormat, args);
    }

    private void OnLocaleChanged(Locale locale) => RefreshContent();

    private void RefreshContent()
    {
        RefreshSpecialistButtons();

        var building = targetBuilding;
        bool offerUpgrade = building != null && building.CanUpgrade;
        string bName = building == null ? null : string.IsNullOrEmpty(building.buildingName) ? building.gameObject.name : building.buildingName;
        if (laterButtonLabel != null) laterButtonLabel.text = Text(offerUpgrade ? "s5_later" : "s5_continue");
        if (buildingIcon != null) buildingIcon.enabled = building != null && building.buildingIcon != null;
        if (!offerUpgrade)
        {
            bool atMax = building != null && building.currentLevel >= building.EffectiveMaxLevel;
            if (windowTitle != null) windowTitle.text = Text(atMax ? "s5_max_level" : "s5_no_upgrade_needed");
            if (messageLabel != null)
                messageLabel.text = atMax ? Text("s5_max_level_message", bName) : Text("s5_no_upgrade_message");
            if (missingCoinsText != null) missingCoinsText.gameObject.SetActive(false);
            if (missingMaterialsText != null) missingMaterialsText.gameObject.SetActive(false);
            if (materialCostGroup != null) materialCostGroup.SetActive(false);
            else if (materialCostText != null) materialCostText.gameObject.SetActive(false);
            if (upgradeButton != null) upgradeButton.interactable = false;
            return;
        }

        if (windowTitle != null) windowTitle.text = Text("s5_upgrade_available");
        if (upgradeButtonLabel != null) upgradeButtonLabel.text = Text("s5_upgrade_now");
        if (buildingTitle != null) buildingTitle.text = bName;
        if (buildingIcon != null && building.buildingIcon != null) buildingIcon.sprite = building.buildingIcon;

        string description = building.NextCapacity > building.CurrentCapacity
            ? Text("s5_upgrade_description")
            : TextWithFallback("s5_upgrade_description_slots", "Upgrade to add a worker slot and improve quality.");

        bool slotsIncrease = building.NextWorkerSlots > building.CurrentWorkerSlots;
        if (slotsBefore != null && slotsAfter != null)
        {
            if (slotsLabel != null) slotsLabel.text = TextWithFallback("s5_worker_slots", "Worker slots");
            slotsBefore.text = $"{building.CurrentWorkerSlots}";
            slotsAfter.text = $"{building.NextWorkerSlots}";
        }
        else if (slotsIncrease)
        {
            string slotsLine = TextWithFallback(
                "s5_worker_slots_change",
                "Worker slots: {0} -> {1}",
                building.CurrentWorkerSlots,
                building.NextWorkerSlots);
            description = $"{description}\n{slotsLine}";
        }

        if (buildingDescription != null)
            buildingDescription.text = description;

        if (capacityLabel != null) capacityLabel.text = Text("s5_capacity", bName);
        if (capacityBefore != null) capacityBefore.text = $"{building.CurrentCapacity}";
        if (capacityAfter != null) capacityAfter.text = $"{building.NextCapacity}";

        if (qualityLabel != null) qualityLabel.text = Text("s5_quality", Text("need_" + building.offeredNeed));
        if (qualityBefore != null) qualityBefore.text = FormatQualityTier(building.currentLevel, building.CurrentQualityBonus);
        if (qualityAfter != null) qualityAfter.text = FormatQualityTier(building.currentLevel + 1, building.NextQualityBonus);

        int cost = building.UpgradeCost;
        int materialCost = building.UpgradeMaterialCost;
        int gain = targetBuilding.ResortValueGain;

        if (materialCostText != null)
        {
            if (costText != null) costText.text = $"{cost}";
            materialCostText.text = $"{materialCost}";
            if (materialCostGroup != null) materialCostGroup.SetActive(materialCost > 0);
            else materialCostText.gameObject.SetActive(materialCost > 0);
        }
        else
        {
            if (materialCostGroup != null) materialCostGroup.SetActive(false);
            if (costText != null)
            {
                costText.text = materialCost > 0
                    ? TextWithFallback("s5_cost_coins_materials", "{0} + {1} Materials", cost, materialCost)
                    : $"{cost}";
            }
        }

        if (resortValueText != null) resortValueText.text = Text("s5_resort_gain", gain);

        RefreshAffordability();
    }

    // =====================================================
    // NAJATIE ŠPECIALISTU PRE TLAČIDLO V S5
    // =====================================================

    public bool TryHireSpecialist(VipRequiredRole role)
    {
        var roster = observedRoster != null ? observedRoster : WorkerRoster.Instance;
        if (roster == null || !roster.TryHireSpecialist(role))
            return false;

        RefreshAffordability();
        RefreshSpecialistButtons();
        return true;
    }

    public void OnHireButlerClicked() => TryHireSpecialist(VipRequiredRole.Butler);
    public void OnHireCaptainClicked() => TryHireSpecialist(VipRequiredRole.Captain);

    private void RefreshSpecialistButtons()
    {
        var roster = observedRoster != null ? observedRoster : WorkerRoster.Instance;
        if (roster == null) return;

        RefreshSingleSpecialistButton(roster, VipRequiredRole.Butler, hireButlerButton, hireButlerLabel, hireButlerCostText);
        RefreshSingleSpecialistButton(roster, VipRequiredRole.Captain, hireCaptainButton, hireCaptainLabel, hireCaptainCostText);
    }

    private static void RefreshSingleSpecialistButton(
        WorkerRoster roster,
        VipRequiredRole role,
        Button button,
        TextMeshProUGUI label,
        TextMeshProUGUI costLabel)
    {
        if (button == null && label == null && costLabel == null) return;

        bool hired = roster.HasSpecialist(role);
        bool canHire = roster.CanHireSpecialist(role);
        string name = roster.GetSpecialistName(role);
        int cost = roster.GetSpecialistHireCost(role);

        if (button != null)
            button.interactable = !hired && canHire;

        if (label != null)
            label.text = hired ? $"{name} (Hired)" : $"Hire {name}";

        if (costLabel != null)
            costLabel.text = hired ? "HIRED" : $"{cost}";
    }

    private static string FormatQualityTier(int level, float qualityBonus)
    {
        string baseTier = level switch
        {
            1 => Text("s5_basic"),
            2 => Text("s5_improved"),
            _ => Text("s5_premium")
        };

        int pct = Mathf.RoundToInt(qualityBonus * 100f);
        return pct > 0
            ? TextWithFallback("s5_quality_with_bonus", "{0} (+{1}%)", baseTier, pct)
            : baseTier;
    }

    private void RefreshAffordability()
    {
        if (targetBuilding == null)
        {
            if (upgradeButton != null) upgradeButton.interactable = false;
            return;
        }

        var resources = ResourceManager.Instance;
        int cost = targetBuilding.UpgradeCost;
        int materialCost = targetBuilding.UpgradeMaterialCost;

        int coins = resources != null ? resources.GetResource(ResourceType.Coins) : 0;
        int materials = resources != null ? resources.GetResource(ResourceType.Materials) : 0;

        int missingCoins = Mathf.Max(0, cost - coins);
        int missingMaterials = Mathf.Max(0, materialCost - materials);
        bool canAfford = resources != null && missingCoins == 0 && missingMaterials == 0;

        if (upgradeButton != null)
        {
            upgradeButton.interactable = !purchaseInProgress && !purchaseCompleted &&
                targetBuilding.CanUpgrade && (cost > 0 || materialCost > 0) && canAfford;
        }

        bool canShowMissing = resources != null && targetBuilding.CanUpgrade;
        string missingMaterialsLabel = missingMaterials > 0
            ? TextWithFallback("s5_missing_materials", "Missing: {0} Materials", missingMaterials)
            : string.Empty;

        if (missingMaterialsText != null)
        {
            bool showCoins = canShowMissing && missingCoins > 0;
            bool showMats = canShowMissing && missingMaterials > 0;

            if (missingCoinsText != null)
            {
                missingCoinsText.gameObject.SetActive(showCoins);
                if (showCoins) missingCoinsText.text = Text("s5_missing_coins", missingCoins);
            }

            missingMaterialsText.gameObject.SetActive(showMats);
            if (showMats) missingMaterialsText.text = missingMaterialsLabel;
        }
        else if (missingCoinsText != null)
        {
            bool showCoins = canShowMissing && missingCoins > 0;
            bool showMats = canShowMissing && missingMaterials > 0;
            missingCoinsText.gameObject.SetActive(showCoins || showMats);

            if (showCoins && showMats)
                missingCoinsText.text = $"{Text("s5_missing_coins", missingCoins)}\n{missingMaterialsLabel}";
            else if (showCoins)
                missingCoinsText.text = Text("s5_missing_coins", missingCoins);
            else if (showMats)
                missingCoinsText.text = missingMaterialsLabel;
        }
    }

    private void OnUpgradeClicked()
    {
        if (purchaseInProgress || purchaseCompleted || targetBuilding == null || !targetBuilding.CanUpgrade)
            return;

        var resources = ResourceManager.Instance;
        if (resources == null) return;

        var building = targetBuilding;
        int cost = building.UpgradeCost;
        int materialCost = building.UpgradeMaterialCost;
        int gain = building.ResortValueGain;

        if (!resources.HasEnough(ResourceType.Coins, cost) ||
            !resources.HasEnough(ResourceType.Materials, materialCost))
        {
            RefreshAffordability();
            return;
        }

        purchaseInProgress = true; // SpendResource invokes listeners synchronously.
        bool coinsSpent = false;
        try
        {
            if (cost > 0)
            {
                if (!resources.SpendResource(ResourceType.Coins, cost)) return;
                coinsSpent = true;
            }

            if (materialCost > 0 && !resources.SpendResource(ResourceType.Materials, materialCost))
            {
                if (coinsSpent) resources.AddResource(ResourceType.Coins, cost);
                return;
            }

            building.ApplyUpgrade();
            purchaseCompleted = true;
            resources.AddResource(ResourceType.ResortValue, gain);
            GamePersistence.Instance?.SaveNow();
            CloseWindow();
        }
        finally
        {
            purchaseInProgress = false;
            RefreshAffordability();
        }
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