using System.Collections.Generic;
using UnityEngine;

public class BuildingSpot : MonoBehaviour
{
    [Header("Zoznam dostupných budov (Prefaby)")]
    [Tooltip("Sem presuň prefaby budov z priečinka Prefabs/buildings")]
    public List<GameObject> buildingPrefabs = new List<GameObject>();

    [Header("Hodnota rezortu")]
    [Tooltip("O koľko stúpne hodnota rezortu po postavení budovy")]
    [SerializeField] private int resortValueGain = 100;

    [Header("Stav miesta")]
    public bool isOccupied = false;
    private GameObject spawnedBuilding;

    private WaveManager waveManager;
    private Renderer[] allRenderers;
    private Collider2D[] allColliders;

    private void Awake()
    {
        allRenderers = GetComponentsInChildren<Renderer>(true);
        allColliders = GetComponentsInChildren<Collider2D>(true);
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
    }

    // Počas vlny sa nestavia (pravidlo WR6) -> skryjeme stavenisko
    private void OnWaveStarted(int waveNumber)
    {
        SetSpotActive(false);
    }

    // Po skončení vlny sa neobsadené stavenisko opäť zobrazí
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
        // Ak beží vlna, nedovolíme stavať
        if (waveManager != null && waveManager.Session != null && waveManager.Session.State == WaveState.Running)
        {
            Debug.LogWarning("Počas bežiacej vlny sa nedá stavať!");
            return;
        }

        BuildBuilding(0);
    }

    public void BuildBuilding(int buildingIndex)
    {
        if (isOccupied)
        {
            Debug.LogWarning("Toto miesto je už obsadené!");
            return;
        }

        if (buildingPrefabs == null || buildingPrefabs.Count == 0)
        {
            Debug.LogError("V zozname Building Prefabs nie sú priradené žiadne prefaby!");
            return;
        }

        if (buildingIndex < 0 || buildingIndex >= buildingPrefabs.Count)
        {
            Debug.LogError($"Index {buildingIndex} je mimo rozsahu!");
            return;
        }

        GameObject prefabToBuild = buildingPrefabs[buildingIndex];

        if (prefabToBuild != null)
        {
            spawnedBuilding = Instantiate(prefabToBuild, transform.position, Quaternion.identity);
            isOccupied = true;

            if (ResourceManager.Instance != null)
                ResourceManager.Instance.AddResource(ResourceType.ResortValue, resortValueGain);

            // Úplne deaktivujeme stavenisko po postavení
            gameObject.SetActive(false);

            Debug.Log($"Budova '{prefabToBuild.name}' bola úspešne postavená.");
        }
    }

    public void BuildFirstBuilding() { BuildBuilding(0); }
    public void BuildSecondBuilding() { BuildBuilding(1); }
    public void BuildThirdBuilding() { BuildBuilding(2); }
}