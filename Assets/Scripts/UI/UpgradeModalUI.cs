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
[SerializeField] private TextMeshProUGUI missingCoinsText;

    [Header("Buttons")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button laterButton;

    [Header("No upgrade available")]
    [SerializeField] private TextMeshProUGUI windowTitle;
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private GameObject[] upgradeOnlyContent;
    [SerializeField] private GameObject noUpgradeContent;
    [SerializeField] private TextMeshProUGUI laterButtonLabel;
    private bool presentationCaptured;
    private string originalTitle, originalLaterLabel;
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

        FindReferencesIfMissing();
    }

    private void CapturePresentation()
    {
        if (presentationCaptured) return;
        presentationCaptured = true;
        originalTitle = windowTitle != null ? windowTitle.text : "UPGRADE AVAILABLE!";
        if (windowTitle != null) originalHeaderSize = windowTitle.rectTransform.sizeDelta;
        originalLaterLabel = laterButtonLabel != null ? laterButtonLabel.text : "LATER";
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
        if (windowTitle != null) windowTitle.text = hasRecommendation ? originalTitle : "NO UPGRADE AVAILABLE";
        if (windowTitle != null) windowTitle.rectTransform.sizeDelta = hasRecommendation ? originalHeaderSize :
            new Vector2(Mathf.Max(originalHeaderSize.x, originalWindowSize.x - 100), originalHeaderSize.y);
        if (windowRect != null) windowRect.sizeDelta = hasRecommendation ? originalWindowSize : new Vector2(originalWindowSize.x, 320f);
        for (int i = 0; i < originalContentVisible.Length; i++)
            if (upgradeOnlyContent[i] != null) upgradeOnlyContent[i].SetActive(hasRecommendation && originalContentVisible[i]);
        if (noUpgradeContent != null) noUpgradeContent.SetActive(!hasRecommendation);
        if (laterButtonLabel != null) laterButtonLabel.text = hasRecommendation ? originalLaterLabel : "CONTINUE";
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
        observedResources = ResourceManager.Instance;
        if (observedResources != null) observedResources.OnResourceChanged += OnCoinsChanged;
        RefreshAffordability();
    }

    private void OnCoinsChanged(ResourceType type, int amount)
    {
        if (type == ResourceType.Coins) RefreshAffordability();
    }

    private void OnDisable()
    {
        if (observedResources != null) observedResources.OnResourceChanged -= OnCoinsChanged;
        observedResources = null;
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
        SetRecommendationPresentation(building != null);
        if (buildingIcon != null) buildingIcon.enabled = building != null && building.buildingIcon != null;
        if (building == null)
        {
            if (missingCoinsText != null) missingCoinsText.gameObject.SetActive(false);
            if (upgradeButton != null) upgradeButton.interactable = false;
            if (modalWindow != null) modalWindow.Open(); else gameObject.SetActive(true);
            return;
        }

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

        RefreshAffordability();

        if (modalWindow != null)
            modalWindow.Open();
        else
            gameObject.SetActive(true);
    }

    private void RefreshAffordability()
    {
        if (targetBuilding == null) { if (upgradeButton != null) upgradeButton.interactable = false; return; }
        var resources = ResourceManager.Instance;
        int cost = targetBuilding.UpgradeCost;
        int coins = resources != null ? resources.GetResource(ResourceType.Coins) : 0;
        int missing = Mathf.Max(0, cost - coins);
        if (upgradeButton != null)
            upgradeButton.interactable = !purchaseInProgress && !purchaseCompleted &&
                targetBuilding.CanUpgrade && cost > 0 && resources != null && missing == 0;
        if (missingCoinsText != null)
        {
            bool showMissing = resources != null && targetBuilding.CanUpgrade && missing > 0;
            missingCoinsText.gameObject.SetActive(showMissing);
            if (showMissing) missingCoinsText.text = $"Missing: {missing} Coins";
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
        int gain = building.ResortValueGain;
        purchaseInProgress = true; // SpendResource invokes listeners synchronously.
        try
        {
            if (!resources.SpendResource(ResourceType.Coins, cost)) return;
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
