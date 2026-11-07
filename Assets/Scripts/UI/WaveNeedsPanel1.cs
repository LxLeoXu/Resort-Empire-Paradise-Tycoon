using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class WaveNeedsPanel1 : MonoBehaviour
{
    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private TextMeshProUGUI guestsBadgeText;

    [Header("S4 Results - Claim Rewards")]
    [SerializeField] private ClaimRewards claimRewards;

    [Header("Karticky potrieb v paneli")]
    [SerializeField]
    private List<NeedCardUI> needCards = new List<NeedCardUI>();

    // Interne sledovanie stavu kazdej potreby
    private class NeedState
    {
        public int totalNeeded;
        public int arrivedCount;
        public int servedCount;

        public float timeAboveThreshold;
        public bool isWarningActive;
    }

    private readonly Dictionary<NeedType, NeedState> states = new();

    private CanvasGroup canvasGroup;

    private int totalWaveNeeds;
    private int totalServedNeeds;

    private const int BottleneckThreshold = 4;
    private const float BottleneckHysteresis = 3.0f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();

        SetPanelVisible(false);
    }

    private void Start()
    {
        // Pripojenie na udalosti WaveManagera
        if (waveManager != null && waveManager.Session != null)
        {
            waveManager.Session.WaveStarted -= OnWaveStarted;
            waveManager.Session.WaveStarted += OnWaveStarted;

            waveManager.Session.WaveEnded -= OnWaveEnded;
            waveManager.Session.WaveEnded += OnWaveEnded;

            waveManager.Session.NeedArrived -= OnNeedArrived;
            waveManager.Session.NeedArrived += OnNeedArrived;

            waveManager.Session.NeedServed -= OnNeedServed;
            waveManager.Session.NeedServed += OnNeedServed;
        }
        else
        {
            Debug.LogError(
                "WaveNeedsPanel: WaveManager alebo Session chyba!",
                this
            );
        }

        // Pripojenie na Claim Rewards
        if (claimRewards != null)
        {
            claimRewards.OnClaimed -= OnRewardsClaimed;
            claimRewards.OnClaimed += OnRewardsClaimed;

            Debug.Log("WaveNeedsPanel: ClaimRewards pripojeny.");
        }
        else
        {
            Debug.LogWarning(
                "WaveNeedsPanel: ClaimRewards nie je priradeny!",
                this
            );
        }
    }

    private void OnDestroy()
    {
        // Odpojenie udalosti WaveManagera
        if (waveManager != null && waveManager.Session != null)
        {
            waveManager.Session.WaveStarted -= OnWaveStarted;
            waveManager.Session.WaveEnded -= OnWaveEnded;
            waveManager.Session.NeedArrived -= OnNeedArrived;
            waveManager.Session.NeedServed -= OnNeedServed;
        }

        // Odpojenie udalosti ClaimRewards
        if (claimRewards != null)
        {
            claimRewards.OnClaimed -= OnRewardsClaimed;
        }
    }

    private void Update()
    {
        if (waveManager == null ||
            waveManager.Session == null ||
            waveManager.Session.State != WaveState.Running)
        {
            return;
        }

        // Kontrola rastu radu a varovani
        foreach (var kvp in states)
        {
            NeedType type = kvp.Key;
            NeedState state = kvp.Value;

            NeedCardUI card = GetCard(type);

            if (card == null || !card.gameObject.activeSelf)
                continue;

            int queue = state.arrivedCount - state.servedCount;

            if (queue > BottleneckThreshold)
            {
                state.timeAboveThreshold += Time.deltaTime;

                if (state.timeAboveThreshold >= BottleneckHysteresis &&
                    !state.isWarningActive)
                {
                    state.isWarningActive = true;

                    string buildingName = GetBuildingDisplayName(type);

                    card.SetWarning(
                        true,
                        $"{buildingName} Queue Growing"
                    );
                }
            }
            else
            {
                state.timeAboveThreshold = 0f;

                if (state.isWarningActive)
                {
                    state.isWarningActive = false;
                    card.SetWarning(false);
                }
            }
        }
    }

    // =====================================
    // ZACIATOK VLNY
    // =====================================

    private void OnWaveStarted(int waveNumber)
    {
        Debug.Log($"S2: Wave {waveNumber} started.");

        // Zobrazime S2
        SetPanelVisible(true);

        // Skryjeme pluska pocas vlny
        SetBuildingSpotsVisible(false);

        states.Clear();

        totalServedNeeds = 0;
        totalWaveNeeds = 0;

        BuildingService[] services =
            FindObjectsByType<BuildingService>(
                FindObjectsSortMode.None
            );

        Dictionary<NeedType, bool> builtNeeds = new();

        foreach (var s in services)
        {
            if (s.Need != NeedType.None)
            {
                builtNeeds[s.Need] = s.HasWorker;
            }
        }

        var waveDef =
            waveManager.Session.GetCurrentWaveDefinition();

        foreach (var card in needCards)
        {
            if (card == null)
                continue;

            bool isBuilt = builtNeeds.ContainsKey(card.needType);

            card.gameObject.SetActive(isBuilt);

            if (isBuilt)
            {
                int countForThisWave = waveDef != null
                    ? waveDef.GetCount(card.needType)
                    : 0;

                states[card.needType] = new NeedState
                {
                    totalNeeded = countForThisWave,
                    arrivedCount = 0,
                    servedCount = 0,
                    timeAboveThreshold = 0f,
                    isWarningActive = false
                };

                totalWaveNeeds += countForThisWave;

                card.UpdateProgress(0, countForThisWave);
                card.SetWarning(false);

                bool hasWorker = builtNeeds[card.needType];

                card.SetWorkerPresent(hasWorker);
            }
        }

        UpdateGuestsBadge();
    }

    // =====================================
    // PRICHOD POTREBY
    // =====================================

    private void OnNeedArrived(WaveNeed need)
    {
        if (states.TryGetValue(need.Type, out var state))
        {
            state.arrivedCount++;

            NeedCardUI card = GetCard(need.Type);

            if (card != null)
            {
                card.UpdateProgress(
                    state.servedCount,
                    state.totalNeeded
                );
            }
        }
    }

    // =====================================
    // OBSLUZENIE POTREBY
    // =====================================

    private void OnNeedServed(WaveNeed need)
    {
        if (states.TryGetValue(need.Type, out var state))
        {
            state.servedCount++;
            totalServedNeeds++;

            NeedCardUI card = GetCard(need.Type);

            if (card != null)
            {
                card.UpdateProgress(
                    state.servedCount,
                    state.totalNeeded
                );
            }

            UpdateGuestsBadge();
        }
    }

    // =====================================
    // KONIEC VLNY - S2 -> S4
    // =====================================

    private void OnWaveEnded(int waveNumber)
    {
        Debug.Log($"S2: Wave {waveNumber} ended.");

        // Skryjeme S2
        SetPanelVisible(false);

        if (claimRewards != null)
        {
            // Pluska zostanu skryte pocas S4.
            SetBuildingSpotsVisible(false);

            Debug.Log(
                "S2 skryta. Cakame na Claim Rewards."
            );
        }
        else
        {
            // Zalozne spravanie, ak ClaimRewards chyba.
            Debug.LogWarning(
                "ClaimRewards nie je priradeny. " +
                "Obnovujem stavebne miesta.",
                this
            );

            SetBuildingSpotsVisible(true);
        }
    }

    // =====================================
    // CLAIM REWARDS - NAVRAT NA OSTROV
    // =====================================

    private void OnRewardsClaimed()
    {
        Debug.Log(
            "Claim Rewards uspesny - navrat na hlavny ostrov."
        );

        // S2 zostane skryta
        SetPanelVisible(false);

        // Zobrazime stavebne miesta
        SetBuildingSpotsVisible(true);
    }

    // =====================================
    // UI - POCET POTRIEB
    // =====================================

    private void UpdateGuestsBadge()
    {
        if (guestsBadgeText != null)
        {
            guestsBadgeText.text =
                $"GUESTS: {totalServedNeeds}/{totalWaveNeeds}";
        }
    }

    private NeedCardUI GetCard(NeedType type)
    {
        return needCards.Find(
            c => c != null && c.needType == type
        );
    }

    private string GetBuildingDisplayName(NeedType type)
        => type switch
        {
            NeedType.Rooms => "Reception",
            NeedType.Food => "Restaurant",
            NeedType.Drinks => "Beach Bar",
            NeedType.Relax => "Pool",
            NeedType.OceanView => "Bungalows",
            _ => "Building"
        };

    // =====================================
    // ZOBRAZENIE A SKRYTIE S2
    // =====================================

    private void SetPanelVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }

    // =====================================
    // ZOBRAZENIE STAVEBNYCH MIEST
    // =====================================

    private void SetBuildingSpotsVisible(bool visible)
    {
        BuildingSpot[] spots =
            FindObjectsByType<BuildingSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (var spot in spots)
        {
            if (spot != null && !spot.isOccupied)
            {
                spot.gameObject.SetActive(visible);
            }
        }
    }

    // =====================================
    // TESTOVANIE
    // =====================================

    [ContextMenu("Test Simulate Serving 1 Food")]
    public void TestServeFood()
    {
        if (states.TryGetValue(
                NeedType.Food,
                out var state) &&
            state.totalNeeded > 0)
        {
            OnNeedServed(
                new WaveNeed(
                    999,
                    1,
                    NeedType.Food,
                    0
                )
            );
        }
    }
}