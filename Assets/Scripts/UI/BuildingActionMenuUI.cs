using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingActionMenuUI : MonoBehaviour
{
    public static BuildingActionMenuUI Instance { get; private set; }

    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private UpgradeModalUI upgradeModal;
    [SerializeField] private WorkerAssignUI workerAssign;
    [SerializeField] private GameObject menuRoot;

    [Header("UI Prvky")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button moveWorkerButton;
    [SerializeField] private Button closeButton;

    private BuildingInfo selectedBuilding;

    private void Awake()
    {
        Instance = this;

        if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
        if (upgradeModal == null) upgradeModal = FindObjectOfType<UpgradeModalUI>();

        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (moveWorkerButton != null) moveWorkerButton.onClick.AddListener(OnMoveWorkerClicked);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    public void Open(BuildingInfo building)
    {
        if (building == null || !building.isBuilt) return;

        selectedBuilding = building;

        if (titleText != null)
            titleText.text = $"{building.buildingName} (Lv. {building.currentLevel})";

        bool isWaveRunning = waveManager != null &&
                             waveManager.Session != null &&
                             waveManager.Session.State == WaveState.Running;

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(!isWaveRunning && building.CanUpgrade);
        }

        if (moveWorkerButton != null)
        {
            moveWorkerButton.gameObject.SetActive(true);
        }

        if (menuRoot != null)
            menuRoot.SetActive(true);
        else
            gameObject.SetActive(true);
    }

    public void Close()
    {
        if (menuRoot != null)
            menuRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void OnUpgradeClicked()
    {
        Close();

        if (upgradeModal == null)
            upgradeModal = FindObjectOfType<UpgradeModalUI>(true);

        if (upgradeModal != null && selectedBuilding != null)
        {
            upgradeModal.OpenForBuilding(selectedBuilding);
        }
        else
        {
            Debug.LogError("Nepodarilo sa nájsť UpgradeModalUI (S5) v scéne!");
        }
    }

    private void OnMoveWorkerClicked()
    {
        Close();

        if (workerAssign == null)
            workerAssign = FindFirstObjectByType<WorkerAssignUI>(FindObjectsInactive.Include);

        if (workerAssign != null && selectedBuilding != null)
        {
            workerAssign.Open(selectedBuilding);
        }
        else
        {
            Debug.LogError("Nepodarilo sa nájsť WorkerAssignUI (S3) v scéne!");
        }
    }
}