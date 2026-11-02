using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class WaveNeedsPanel : MonoBehaviour
{
    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private TextMeshProUGUI guestsBadgeText;

    [Header("Kartičky potrieb v paneli")]
    [SerializeField] private List<NeedCardUI> needCards = new List<NeedCardUI>();

    // Interné sledovanie stavu každej potreby
    private class NeedState
    {
        public int totalNeeded;
        public int arrivedCount;
        public int servedCount;
        public float timeAboveThreshold; // Čas, koľko je rad > 4
        public bool isWarningActive;
    }

    private readonly Dictionary<NeedType, NeedState> states = new();
    private CanvasGroup canvasGroup;
    private int totalWaveNeeds;
    private int totalServedNeeds;

    private const int BottleneckThreshold = 4;      // Rad nad 4 hostí
    private const float BottleneckHysteresis = 3.0f; // Musí trvať aspoň 3 sekundy

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (waveManager == null)
            waveManager = FindObjectOfType<WaveManager>();

        SetPanelVisible(false);
    }

    private void Start()
    {
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
    }

    private void OnDestroy()
    {
        if (waveManager != null && waveManager.Session != null)
        {
            waveManager.Session.WaveStarted -= OnWaveStarted;
            waveManager.Session.WaveEnded -= OnWaveEnded;
            waveManager.Session.NeedArrived -= OnNeedArrived;
            waveManager.Session.NeedServed -= OnNeedServed;
        }
    }

    private void Update()
    {
        // Kontrola rastu radu a časovača 3 sekundy pre varovanie
        if (waveManager == null || waveManager.Session == null || waveManager.Session.State != WaveState.Running)
            return;

        foreach (var kvp in states)
        {
            NeedType type = kvp.Key;
            NeedState state = kvp.Value;
            NeedCardUI card = GetCard(type);
            if (card == null || !card.gameObject.activeSelf) continue;

            int queue = state.arrivedCount - state.servedCount;

            if (queue > BottleneckThreshold)
            {
                state.timeAboveThreshold += Time.deltaTime;
                if (state.timeAboveThreshold >= BottleneckHysteresis && !state.isWarningActive)
                {
                    state.isWarningActive = true;
                    string buildingName = GetBuildingDisplayName(type);
                    card.SetWarning(true, $"{buildingName} Queue Growing");
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

    private void OnWaveStarted(int waveNumber)
    {
        SetPanelVisible(true);
        SetBuildingSpotsVisible(false); // <-- SKRYJE PLUSKÁ POČAS VLNY
        states.Clear();
        totalServedNeeds = 0;
        totalWaveNeeds = 0;

        BuildingService[] services = FindObjectsOfType<BuildingService>();
        Dictionary<NeedType, bool> builtNeeds = new();
        foreach (var s in services)
        {
            if (s.Need != NeedType.None)
            {
                builtNeeds[s.Need] = s.HasWorker;
            }
        }

        var waveDef = waveManager.Session.GetCurrentWaveDefinition();

        foreach (var card in needCards)
        {
            if (card == null) continue;

            bool isBuilt = builtNeeds.ContainsKey(card.needType);
            card.gameObject.SetActive(isBuilt);

            if (isBuilt)
            {
                int countForThisWave = waveDef != null ? waveDef.GetCount(card.needType) : 0;

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

                // Nastavenie prítomnosti workera podľa budovy
                bool hasWorker = builtNeeds[card.needType];
                card.SetWorkerPresent(hasWorker);
            }
        }

        UpdateGuestsBadge();
    }

    private void OnNeedArrived(WaveNeed need)
    {
        if (states.TryGetValue(need.Type, out var state))
        {
            state.arrivedCount++;
            NeedCardUI card = GetCard(need.Type);
            if (card != null)
            {
                card.UpdateProgress(state.servedCount, state.totalNeeded);
            }
        }
    }

    private void OnNeedServed(WaveNeed need)
    {
        if (states.TryGetValue(need.Type, out var state))
        {
            state.servedCount++;
            totalServedNeeds++;

            NeedCardUI card = GetCard(need.Type);
            if (card != null)
            {
                card.UpdateProgress(state.servedCount, state.totalNeeded);
            }

            UpdateGuestsBadge();
        }
    }

    private void OnWaveEnded(int waveNumber)
    {
        SetPanelVisible(false);
        SetBuildingSpotsVisible(true); // <-- ZNOVA ZOBRAZÍ PLUSKÁ PO VLNE
    }

    private void UpdateGuestsBadge()
    {
        if (guestsBadgeText != null)
        {
            guestsBadgeText.text = $"GUESTS: {totalServedNeeds}/{totalWaveNeeds}";
        }
    }

    private NeedCardUI GetCard(NeedType type)
    {
        return needCards.Find(c => c != null && c.needType == type);
    }

    private string GetBuildingDisplayName(NeedType type) => type switch
    {
        NeedType.Rooms => "Reception",
        NeedType.Food => "Restaurant",
        NeedType.Drinks => "Beach Bar",
        NeedType.Relax => "Pool",
        NeedType.OceanView => "Bungalows",
        _ => "Building"
    };

    private void SetPanelVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
    }

    [ContextMenu("Test Simulate Serving 1 Food")]
    public void TestServeFood()
    {
        if (states.TryGetValue(NeedType.Food, out var state) && state.totalNeeded > 0)
        {
            OnNeedServed(new WaveNeed(999, 1, NeedType.Food, 0));
        }
    }
    private void SetBuildingSpotsVisible(bool visible)
    {
        // Nájde všetky staveniská na ostrove
        BuildingSpot[] spots = FindObjectsOfType<BuildingSpot>(true);
        foreach (var spot in spots)
        {
            // Vypne alebo zapne len tie, kde ešte nestojí budova
            if (spot != null && !spot.isOccupied)
            {
                spot.gameObject.SetActive(visible);
            }
        }
    }


}