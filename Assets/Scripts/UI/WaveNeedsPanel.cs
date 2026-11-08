using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class WaveNeedsPanel : MonoBehaviour
{
    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private TextMeshProUGUI guestsBadgeText;

    [Header("S4 - Results Integration")]
    [SerializeField] private ClaimRewards claimRewards;
    [SerializeField] private S4FigmaResultsUI resultsUI;

    [Header("Karticky potrieb v paneli")]
    [SerializeField]
    private List<NeedCardUI> needCards = new List<NeedCardUI>();

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
    private WorkerRoster roster;

    private int totalWaveNeeds;
    private int totalServedNeeds;

    // SCRUM-92: Stav prechodu S2 -> S4.
    private bool waitingForClaim;
    private bool? buildingSpotsVisible;

    private const int BottleneckThreshold = 4;
    private const float BottleneckHysteresis = 3.0f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();

        // Volitelne automaticke doplnenie referencii.
        if (resultsUI == null && claimRewards != null)
            resultsUI = claimRewards.GetComponent<S4FigmaResultsUI>();

        if (claimRewards == null && resultsUI != null)
            claimRewards = resultsUI.GetComponent<ClaimRewards>();

        SetPanelVisible(false);
    }

    private void Start()
    {
        // Povodne udalosti S2.
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

            waveManager.Session.ArrivalEnded -= OnArrivalEnded;
            waveManager.Session.ArrivalEnded += OnArrivalEnded;
        }
        else
        {
            Debug.LogError(
                "WaveNeedsPanel: WaveManager alebo Session chyba!",
                this);
        }

        // Kolegov WorkerRoster zachovame.
        roster = WorkerRoster.Instance;

        if (roster != null)
        {
            roster.Changed -= RefreshWorkers;
            roster.Changed += RefreshWorkers;
        }
        else
        {
            Debug.LogWarning(
                "WaveNeedsPanel: WorkerRoster nie je dostupny.",
                this);
        }

        // SCRUM-303: Reakcia na Claim Rewards.
        if (claimRewards != null)
        {
            claimRewards.OnClaimed -= OnRewardsClaimed;
            claimRewards.OnClaimed += OnRewardsClaimed;

            Debug.Log("S2: ClaimRewards pripojeny.");
        }
        else
        {
            Debug.LogWarning(
                "S2: ClaimRewards nie je priradeny!",
                this);
        }
    }

    private void OnDestroy()
    {
        if (waveManager != null && waveManager.Session != null)
        {
            waveManager.Session.WaveStarted -= OnWaveStarted;
            waveManager.Session.WaveEnded -= OnWaveEnded;
            waveManager.Session.NeedArrived -= OnNeedArrived;
            waveManager.Session.NeedServed -= OnNeedServed;
            waveManager.Session.ArrivalEnded -= OnArrivalEnded;
        }

        if (roster != null)
            roster.Changed -= RefreshWorkers;

        if (claimRewards != null)
            claimRewards.OnClaimed -= OnRewardsClaimed;
    }

    private void Update()
    {
        if (waveManager == null ||
            waveManager.Session == null ||
            waveManager.Session.State != WaveState.Running)
            return;

        foreach (var kvp in states)
        {
            NeedType type = kvp.Key;
            NeedState state = kvp.Value;

            NeedCardUI card = GetCard(type);

            if (card == null || !card.gameObject.activeSelf)
                continue;

            // Zachovavame kolegov skutocny pocet cakajucich.
            int queue = waveManager.Session.GetQueueCount(type);

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
                        $"{buildingName} Queue Growing");
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

    // SCRUM-92:
    // Ak S4 nie je dostupna, vratime stavebne miesta.
    // Ak sa S4 zobrazi neskor, pluska znova skryjeme.
    private void LateUpdate()
    {
        if (!waitingForClaim || resultsUI == null)
            return;

        bool s4Visible = resultsUI.gameObject.activeInHierarchy;

        SetBuildingSpotsVisible(!s4Visible);
    }

    // =====================================
    // ZACIATOK VLNY
    // =====================================

    private void OnWaveStarted(int waveNumber)
    {
        Debug.Log($"S2: Wave {waveNumber} started.");

        waitingForClaim = false;

        SetPanelVisible(true);
        SetBuildingSpotsVisible(false);

        states.Clear();

        totalServedNeeds = 0;
        totalWaveNeeds = 0;

        BuildingService[] services =
            FindObjectsByType<BuildingService>(
                FindObjectsSortMode.None);

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
    // WORKER ROSTER - KOLEGOVA LOGIKA
    // =====================================

    private void RefreshWorkers()
    {
        BuildingService[] services =
            FindObjectsByType<BuildingService>(
                FindObjectsSortMode.None);

        foreach (var s in services)
        {
            if (s.Need == NeedType.None)
                continue;

            NeedCardUI card = GetCard(s.Need);

            if (card != null)
                card.SetWorkerPresent(s.HasWorker);
        }
    }

    // =====================================
    // PRICHOD HOSTA / POTREBY
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
                    state.totalNeeded);
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
                    state.totalNeeded);
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

        // S2 sa skryje.
        SetPanelVisible(false);

        // S4 otvara existujuci WaveS4Connector.
        if (claimRewards != null && resultsUI != null)
        {
            waitingForClaim = true;

            // Pluska zostanu skryte pocas S4.
            SetBuildingSpotsVisible(false);

            Debug.Log(
                "S2 skryta. Cakame na otvorenie S4 a Claim Rewards.");
        }
        else
        {
            waitingForClaim = false;

            // Bez S4 referencii nezablokujeme ostrov.
            SetBuildingSpotsVisible(true);

            Debug.LogWarning(
                "S2: S4 referencie chybaju. " +
                "Stavebne miesta obnovene.",
                this);
        }
    }

    // =====================================
    // CLAIM REWARDS -> HLAVNY OSTROV
    // =====================================

    private void OnRewardsClaimed()
    {
        Debug.Log(
            "S4: Claim Rewards uspesny. Navrat na ostrov.");

        waitingForClaim = false;

        // S2 zostane skryta.
        SetPanelVisible(false);

        // Znovu zobrazime pluska.
        SetBuildingSpotsVisible(true);
    }

    // =====================================
    // UI - POCET HOSTI / POTRIEB
    // =====================================

    private void UpdateGuestsBadge()
    {
        if (guestsBadgeText == null ||
            waveManager == null ||
            waveManager.Session == null)
            return;

        string status = waveManager.Session.ArrivalsEnded
            ? "All guests arrived"
            : "Guests arriving...";

        guestsBadgeText.text =
            $"{status}\nGUESTS: {totalServedNeeds}/{totalWaveNeeds}";
    }

    private void OnArrivalEnded(int waveNumber)
    {
        UpdateGuestsBadge();
    }

    private NeedCardUI GetCard(NeedType type)
    {
        return needCards.Find(
            c => c != null && c.needType == type);
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
    // ZOBRAZENIE S2
    // =====================================

    private void SetPanelVisible(bool visible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    // =====================================
    // STAVEBNE MIESTA
    // =====================================

    private void SetBuildingSpotsVisible(bool visible)
    {
        // Nevykonavame hladanie kazdy frame,
        // iba pri zmene pozadovaneho stavu.
        if (buildingSpotsVisible.HasValue &&
            buildingSpotsVisible.Value == visible)
            return;

        buildingSpotsVisible = visible;

        BuildingSpot[] spots =
            FindObjectsByType<BuildingSpot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

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
        if (states.TryGetValue(NeedType.Food, out var state) &&
            state.totalNeeded > 0)
        {
            OnNeedServed(
                new WaveNeed(
                    999,
                    1,
                    NeedType.Food,
                    0));
        }
    }
}