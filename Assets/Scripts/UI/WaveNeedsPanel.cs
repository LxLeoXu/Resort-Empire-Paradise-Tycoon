using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class WaveNeedsPanel : MonoBehaviour
{
    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private TextMeshProUGUI guestsBadgeText;

    [Header("Kartičky potrieb v paneli")]
    [SerializeField] private List<NeedCardUI> needCards = new List<NeedCardUI>();

    [Header("Zasúvanie a Animácia")]
    [Tooltip("Priehľadný overlay za panelom na kliknutie mimo")]
    [SerializeField] private Button overlayButton;

    [Tooltip("Modrý pásik na pravej hrane obrazovky na opätovné otvorenie")]
    [SerializeField] private Button toggleTabButton;

    [Tooltip("Rýchlosť zasunutia v sekundách")]
    [SerializeField] private float slideDuration = 0.25f;

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
    private RectTransform panelRect;
    private WorkerRoster roster;
    private Coroutine slideCoroutine;

    private Vector2 openedPosition;
    private Vector2 closedPosition;
    private bool isCollapsed = false;

    private int totalWaveNeeds;
    private int totalServedNeeds;

    private const int BottleneckThreshold = 2;
    private const float BottleneckHysteresis = 1.0f;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        panelRect = GetComponent<RectTransform>();

        if (waveManager == null)
            waveManager = FindObjectOfType<WaveManager>();

        openedPosition = panelRect.anchoredPosition;
        float panelWidth = panelRect.rect.width > 0 ? panelRect.rect.width : 550f;
        closedPosition = new Vector2(openedPosition.x + panelWidth + 50f, openedPosition.y);

        if (overlayButton != null)
            overlayButton.onClick.AddListener(CollapsePanel);

        if (toggleTabButton != null)
            toggleTabButton.onClick.AddListener(ExpandPanel);

        // KRITICKÁ POISTKA: Na štarte hry VŠETKO vypneme (žiadny overlay nesmie blokovať ostrov!)
        SetPanelVisible(false);
        if (overlayButton != null) overlayButton.gameObject.SetActive(false);
        if (toggleTabButton != null) toggleTabButton.gameObject.SetActive(false);
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

        roster = WorkerRoster.Instance;
        if (roster != null)
        {
            roster.Changed -= RefreshWorkers;
            roster.Changed += RefreshWorkers;
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

        if (roster != null)
            roster.Changed -= RefreshWorkers;
    }

    private void Update()
    {
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
        states.Clear();
        totalServedNeeds = 0;
        totalWaveNeeds = 0;

        // Vlna začína: panel je otvorený, Overlay sa zapne, pásik je vypnutý
        panelRect.anchoredPosition = openedPosition;
        isCollapsed = false;
        if (overlayButton != null) overlayButton.gameObject.SetActive(true);
        if (toggleTabButton != null) toggleTabButton.gameObject.SetActive(false);

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

                bool hasWorker = builtNeeds[card.needType];
                card.SetWorkerPresent(hasWorker);
            }
        }

        UpdateGuestsBadge();
    }

    private void OnWaveEnded(int waveNumber)
    {
        // Koniec vlny: schováme panel, Overlay aj modrý pásik
        SetPanelVisible(false);
        if (overlayButton != null) overlayButton.gameObject.SetActive(false);
        if (toggleTabButton != null) toggleTabButton.gameObject.SetActive(false);
        isCollapsed = false;
    }

    /// <summary>
    /// Zasunúť panel doprava (kliknutie na Overlay)
    /// </summary>
    public void CollapsePanel()
    {
        // POISTKA: Ak nebeží vlna, Overlay nesmie nič robiť!
        if (waveManager == null || waveManager.Session == null || waveManager.Session.State != WaveState.Running)
            return;

        if (isCollapsed) return;
        isCollapsed = true;

        if (overlayButton != null) overlayButton.gameObject.SetActive(false);
        if (toggleTabButton != null) toggleTabButton.gameObject.SetActive(false);

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(AnimateSlide(closedPosition, true));
    }

    /// <summary>
    /// Vysunúť panel späť doľava (kliknutie na modrý pásik)
    /// </summary>
    public void ExpandPanel()
    {
        if (!isCollapsed) return;
        isCollapsed = false;

        // Pásik hneď schováme
        if (toggleTabButton != null) toggleTabButton.gameObject.SetActive(false);

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(AnimateSlide(openedPosition, false));
    }

    private IEnumerator AnimateSlide(Vector2 targetPos, bool collapsing)
    {
        Vector2 startPos = panelRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        panelRect.anchoredPosition = targetPos;

        // AŽ TERAZ po dobehnutí animácie aktivujeme správny prvok:
        if (collapsing)
        {
            // Panel sa úspešne skryl -> ZOBRAZÍME MODRÝ PÁSIK SO ŠÍPKOU
            if (toggleTabButton != null) toggleTabButton.gameObject.SetActive(true);
        }
        else
        {
            // Panel sa úspešne vrátil -> ZAPNEME OVERLAY
            if (overlayButton != null) overlayButton.gameObject.SetActive(true);
        }
    }

    public void RefreshWorkers()
    {
        BuildingService[] services = FindObjectsOfType<BuildingService>();
        foreach (var s in services)
        {
            if (s.Need == NeedType.None) continue;
            NeedCardUI card = GetCard(s.Need);
            if (card != null)
                card.SetWorkerPresent(s.HasWorker);
        }
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
}