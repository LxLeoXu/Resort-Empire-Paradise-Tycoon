//using TMPro;
//using UnityEngine;
//using UnityEngine.UI;

//public class BuildingActionMenuUI : MonoBehaviour
//{
//    public static BuildingActionMenuUI Instance { get; private set; }

//    [Header("Referencie")]
//    [SerializeField] private WaveManager waveManager;
//    [SerializeField] private UpgradeModalUI upgradeModal;
//    [SerializeField] private GameObject menuRoot; // Objekt, ktorý sa zapína/vypína

//    [Header("UI Prvky")]
//    [SerializeField] private TextMeshProUGUI titleText;
//    [SerializeField] private Button upgradeButton;
//    [SerializeField] private Button moveWorkerButton;
//    [SerializeField] private Button closeButton;

//    private BuildingInfo selectedBuilding;

//    private void Awake()
//    {
//        Instance = this;

//        if (waveManager == null) waveManager = FindObjectOfType<WaveManager>();
//        if (upgradeModal == null) upgradeModal = FindObjectOfType<UpgradeModalUI>();

//        if (upgradeButton != null) upgradeButton.onClick.AddListener(OnUpgradeClicked);
//        if (moveWorkerButton != null) moveWorkerButton.onClick.AddListener(OnMoveWorkerClicked);
//        if (closeButton != null) closeButton.onClick.AddListener(Close);

//        Close();
//    }

//    /// <summary>
//    /// Otvorí menu možností pre kliknutú budovu
//    /// </summary>
//    public void Open(BuildingInfo building)
//    {
//        if (building == null || !building.isBuilt) return;

//        selectedBuilding = building;

//        if (titleText != null)
//            titleText.text = $"{building.buildingName} (Lv. {building.currentLevel})";

//        // Kontrola: Beží vlna?
//        bool isWaveRunning = waveManager != null &&
//                             waveManager.Session != null &&
//                             waveManager.Session.State == WaveState.Running;

//        // Pravidlo hry: Upgrade je dostupný LEN medzi vlnami, počas vlny sa nezobrazuje!
//        if (upgradeButton != null)
//        {
//            upgradeButton.gameObject.SetActive(!isWaveRunning && building.CanUpgrade);
//        }

//        // Presun workera je povolený vždy
//        if (moveWorkerButton != null)
//        {
//            moveWorkerButton.gameObject.SetActive(true);
//        }

//        if (menuRoot != null)
//            menuRoot.SetActive(true);
//        else
//            gameObject.SetActive(true);
//    }

//    public void Close()
//    {
//        if (menuRoot != null)
//            menuRoot.SetActive(false);
//        else
//            gameObject.SetActive(false);
//    }

//    private void OnUpgradeClicked()
//    {
//        Close();
//        if (upgradeModal != null && selectedBuilding != null)
//        {
//            upgradeModal.OpenForBuilding(selectedBuilding);
//        }
//    }

//    private void OnMoveWorkerClicked()
//    {
//        Close();
//        // S3 zatiaľ neexistuje, preto len log
//        Debug.Log($"<color=cyan>[S3 Worker Allocation]</color> Otváram Erikovu S3 pre budovu: <b>{selectedBuilding.buildingName}</b>.");
//    }
//}