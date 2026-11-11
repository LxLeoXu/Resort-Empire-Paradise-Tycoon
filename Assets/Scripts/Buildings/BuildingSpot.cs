using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingSpot : MonoBehaviour
{
    [Header("UI Okno S9")]
    [Tooltip("Sem pretiahni objekt S9_AreaUnlock z Canvasu")]
    [SerializeField] private GameObject s9Window;

    [Header("Nastavenia budovy pre S9")]
    [SerializeField] private string areaTitle = "The Luxury Pool";
    [SerializeField] private Sprite areaPreview;
    [SerializeField] private string demandAdded = "Relax Demand";
    [SerializeField] private int unlockCost = 1200;
    [SerializeField] private int requiredWave = 3;

    [Header("Zoznam dostupných budov (Prefaby)")]
    public List<GameObject> buildingPrefabs = new List<GameObject>();

    [Header("Hodnota rezortu")]
    [SerializeField] private int resortValueGain = 100;

    [Header("Stav miesta")]
    public bool isOccupied = false;
    private GameObject spawnedBuilding;

    private WaveManager waveManager;
    private Renderer[] allRenderers;
    private Collider2D[] allColliders;
    private AreaUnlockUI areaUnlockUI;

    private void Awake()
    {
        allRenderers = GetComponentsInChildren<Renderer>(true);
        allColliders = GetComponentsInChildren<Collider2D>(true);

        if (s9Window != null)
            areaUnlockUI = s9Window.GetComponent<AreaUnlockUI>();
    }

    private void Start()
    {
        waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null && waveManager.Session != null)
        {
            waveManager.Session.WaveStarted += OnWaveStarted;
            waveManager.Session.WaveEnded += OnWaveEnded;
        }
    }

    private void OnDestroy()
    {
        if (waveManager != null && waveManager.Session != null)
        {
            waveManager.Session.WaveStarted -= OnWaveStarted;
            waveManager.Session.WaveEnded -= OnWaveEnded;
        }

        if (areaUnlockUI != null)
            areaUnlockUI.OnUnlockClicked -= HandleBuyAndBuild;
    }

    private void OnWaveStarted(int waveNumber)
    {
        SetSpotActive(false);
    }

    private void OnWaveEnded(int waveNumber)
    {
        if (!isOccupied)
        {
            SetSpotActive(true);
        }
    }

    private void SetSpotActive(bool active)
    {
        foreach (var r in allRenderers)
            if (r != null) r.enabled = active;

        foreach (var c in allColliders)
            if (c != null) c.enabled = active;
    }

    private void OnMouseDown()
    {
        OpenS9();
    }

    public void OpenS9()
    {
        if (waveManager != null && waveManager.Session != null && waveManager.Session.State == WaveState.Running)
        {
            Debug.LogWarning("Počas bežiacej vlny sa nedá stavať!");
            return;
        }

        if (isOccupied) return;

        // Otvoríme o 1 frame neskôr, aby kliknutie mimo S5 nezatvorilo S9
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(OpenS9Routine());
        }
        else
        {
            DoOpenS9();
        }
    }

    private IEnumerator OpenS9Routine()
    {
        yield return null; // Počkáme 1 frame na dokončenie predchádzajúceho kliknutia
        DoOpenS9();
    }

    private void DoOpenS9()
    {
        if (s9Window != null)
        {
            if (areaUnlockUI == null)
                areaUnlockUI = s9Window.GetComponent<AreaUnlockUI>();

            if (areaUnlockUI != null)
            {
                int coins = ResourceManager.Instance != null ? ResourceManager.Instance.GetResource(ResourceType.Coins) : 0;
                
                // Zistíme aktuálnu vlnu (aj v testovacom režime)
                int activeWave = waveManager != null ? waveManager.EffectiveWaveNumber : 0;
                
                // Budova je sprístupnená (Eligible):
                // 1. Ak je aktuálna vlna vyššia ako požadovaná (napr. vo vlne 4 je bazén odomknutý, vo vlne 7 sú bungalovy odomknuté)
                // 2. ALEBO ak sme práve dokončili vlnu 3 / 6 a sme vo výsledkoch
                bool isWaveEnded = waveManager != null && waveManager.Session != null && waveManager.Session.State == WaveState.Result;
                bool isEligible = (activeWave > requiredWave) || (activeWave == requiredWave && isWaveEnded);

                areaUnlockUI.Setup(areaTitle, areaPreview, demandAdded, unlockCost, isOccupied, isEligible, coins);

                areaUnlockUI.OnUnlockClicked -= HandleBuyAndBuild;
                areaUnlockUI.OnUnlockClicked += HandleBuyAndBuild;
            }

            ModalWindow modal = s9Window.GetComponent<ModalWindow>();
            if (modal != null)
                modal.Open();
            else
                s9Window.SetActive(true);

            Debug.Log($"<color=green>[S9 OTVORENÉ]</color> Otvorené S9 pre {areaTitle} (Požadovaná vlna: {requiredWave}, Aktuálna vlna: {(waveManager != null ? waveManager.EffectiveWaveNumber : 0)})");
        }
        else
        {
            Debug.LogError("BuildingSpot nemá priradené s9Window!", this);
        }
    }

    private void HandleBuyAndBuild()
    {
        if (isOccupied) return;

        if (ResourceManager.Instance != null)
        {
            if (!ResourceManager.Instance.SpendResource(ResourceType.Coins, unlockCost))
            {
                Debug.LogWarning("Nemáš dosť mincí na odomknutie!");
                return;
            }
        }

        BuildFirstBuilding();

        if (s9Window != null)
        {
            ModalWindow modal = s9Window.GetComponent<ModalWindow>();
            if (modal != null) modal.Close();
            else s9Window.SetActive(false);
        }
    }

    public void BuildBuilding(int buildingIndex)
    {
        if (isOccupied) return;

        if (buildingPrefabs == null || buildingPrefabs.Count == 0 || buildingIndex >= buildingPrefabs.Count)
        {
            Debug.LogError("Chýba prefab budovy!", this);
            return;
        }

        GameObject prefabToBuild = buildingPrefabs[buildingIndex];

        if (prefabToBuild != null)
        {
            spawnedBuilding = Instantiate(prefabToBuild, transform.position, Quaternion.identity);
            isOccupied = true;

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.AddResource(ResourceType.ResortValue, resortValueGain);

            if (areaUnlockUI != null)
                areaUnlockUI.OnUnlockClicked -= HandleBuyAndBuild;

            gameObject.SetActive(false);
            Debug.Log($"<color=green>[POSTAVENÉ]</color> {prefabToBuild.name} úspešne postavená!");
        }
    }

    public void BuildFirstBuilding() { BuildBuilding(0); }
    public void BuildSecondBuilding() { BuildBuilding(1); }
    public void BuildThirdBuilding() { BuildBuilding(2); }
}