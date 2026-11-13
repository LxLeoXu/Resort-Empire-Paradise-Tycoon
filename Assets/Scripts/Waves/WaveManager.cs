using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class WaveManager : MonoBehaviour
{
    [SerializeField] private WaveCatalog catalog;
    [Min(0.01f)][SerializeField] private float arrivalDuration = 90f;
    [Tooltip("SCRUM-235: connect S4 to this event. Argument: completed wave number (1-based).")]
    [SerializeField] private UnityEvent<int> onWaveEnded = new();

    [Header("SCRUM-25 & Progresia")]
    [Tooltip("Pomer nového needu pri mäkkom debute (Build dok §4.8: 0.50)")]
    [Range(0.1f, 1f)][SerializeField] private float newNeedDebutRatio = 0.5f;

    [Header("Testovanie vĺn")]
    [Tooltip("Zaškrtni, ak chceš manuálne testovať konkrétnu vlnu.")]
    [SerializeField] private bool useTestWave = false;
    [Tooltip("Číslo vlny na testovanie (1 - 8).")]
    [Range(1, 8)][SerializeField] private int testWaveNumber = 1;

    [Tooltip("TEST KRITÉRIA: Zaškrtni, ak testuješ neskorú kúpu bazéna (Relax debutuje až v tejto testovanej vlne).")]
    [SerializeField] private bool testLatePoolDebut = false;

    // Sledovanie debutov
    private bool hasRelaxDebuted = false;
    private bool hasOceanViewDebuted = false;

    private WaveDefinition lastExecutedWave = null;

    public WaveSession Session { get; private set; }
    public GuestWavePresentation Guests { get; private set; }
    public UnityEvent<int> OnWaveEnded => onWaveEnded;

    // Vlastnosti pre ostatné skripty (BuildingSpot, S5)
    public bool UseTestWave => useTestWave;
    public int TestWaveNumber => testWaveNumber;
    public int EffectiveWaveNumber => useTestWave ? testWaveNumber : (Session != null ? Session.WaveNumber : 1);

    private void Awake()
    {
        if (catalog == null)
        {
            Debug.LogError("WaveManager: Assign a Wave Catalog in the Inspector.", this);
            enabled = false;
            return;
        }

        Session = new WaveSession(catalog);
        Session.WaveEnded += OnWaveFinishedInternal;
        Guests = GetComponent<GuestWavePresentation>();

        // Inicializácia testovacej vlny hneď pri štarte hry
        if (useTestWave)
        {
            Session.SetWaveIndex(testWaveNumber - 1);
            SyncDebutsForTestWave(testWaveNumber);
            Debug.Log($"<color=cyan>[TESTOVACÍ REŽIM]</color> Inicializovaná testovacia vlna č. {testWaveNumber}");
        }
    }

    private void Update()
    {
        if (Session == null) return;
        if (Guests != null && Guests.enabled)
        {
            if (!Guests.BoatDocked) return;
            Session.HoldArrivalsOpen = !Guests.AllGuestsDisembarked;
        }
        Session.Tick(Time.deltaTime);
    }

    public void StartWave()
    {
        if (Session == null || Session.State == WaveState.Running) return;
        if (Guests != null && Guests.enabled && Guests.TripInProgress) return;

        int waveIndex = Session.WaveNumber;
        if (useTestWave)
        {
            waveIndex = testWaveNumber - 1;
            Session.SetWaveIndex(waveIndex);
            SyncDebutsForTestWave(testWaveNumber);
        }

        HashSet<NeedType> availableNeeds = GetBuiltBuildingNeeds();

        WaveDefinition waveToRun = CalculateAdaptedWave(waveIndex, availableNeeds, logDetails: true);
        lastExecutedWave = waveToRun;

        Session.StartWave(arrivalDuration, availableNeeds, waveToRun);
    }

    public WaveDefinition CalculateAdaptedWave(int waveIndex, HashSet<NeedType> availableNeeds, bool logDetails = false)
    {
        if (catalog == null || waveIndex < 0 || waveIndex >= catalog.Count) return null;

        WaveDefinition baseWave = catalog.GetWave(waveIndex);
        WaveDefinition prevWave = waveIndex > 0 ? (lastExecutedWave ?? catalog.GetWave(waveIndex - 1)) : null;

        bool isRelaxDebut = availableNeeds.Contains(NeedType.Relax) && !hasRelaxDebuted;
        bool isOceanViewDebut = availableNeeds.Contains(NeedType.OceanView) && !hasOceanViewDebuted;

        if ((!isRelaxDebut && !isOceanViewDebut) || prevWave == null)
        {
            return baseWave;
        }

        // --- MÄKKÝ DEBUT BAZÉNA (Relax) ---
        if (isRelaxDebut)
        {
            // 4. VLNA (waveIndex 3): Už v tabuľke má nastavených presne 8 ks! 
            // Nesmieme to znova deliť 0.5, použijeme priamo hodnotu z tabuľky:
            if (waveIndex <= 3)
            {
                if (logDetails)
                    Debug.Log("<color=green>[DEBUT NAČAS]</color> Relax debutuje vo 4. vlne presne podľa tabuľky (8 ks).");
                return baseWave;
            }

            // NESKORÁ KÚPA BAZÉNA (až vo vlne 5, 6...):
            // Vtedy vezmeme plný objem danej vlny (napr. vo vlne 5 je to 16) a dáme mu 50 % (= 8 ks)
            int relaxCount = Mathf.Max(1, Mathf.RoundToInt(baseWave.relax * newNeedDebutRatio));

            // Pravidlo 3: Ostatné potreby nerastú - držia hodnoty z predošlej vlny
            int rooms = prevWave.rooms;
            int drinks = prevWave.drinks;
            int food = prevWave.food;
            int oceanView = availableNeeds.Contains(NeedType.OceanView) ? prevWave.oceanView : 0;
            int totalNeeds = rooms + drinks + food + relaxCount + oceanView;
            int guests = Mathf.Max(1, Mathf.RoundToInt(totalNeeds / 2.0f));

            if (logDetails)
            {
                Debug.Log($"<color=yellow>[MÄKKÝ DEBUT BAZÉNA - Vlna {waveIndex + 1}]</color> Relax: {relaxCount} ks. Ostatné potreby zmrazené z predošlej vlny (Rooms: {rooms}, Drinks: {drinks}, Food: {food}).");
            }

            return new WaveDefinition(guests, rooms, drinks, food, relaxCount, oceanView);
        }

        // --- MÄKKÝ DEBUT BUNGALOVY (Ocean View) ---
        if (isOceanViewDebut)
        {
            // 7. VLNA (waveIndex 6): Už v tabuľke má nastavených presne 10 ks!
            if (waveIndex <= 6)
            {
                if (logDetails)
                    Debug.Log("<color=green>[DEBUT NAČAS]</color> Ocean View debutuje v 7. vlne presne podľa tabuľky (10 ks).");
                return baseWave;
            }

            int oceanViewCount = Mathf.Max(1, Mathf.RoundToInt(baseWave.oceanView * newNeedDebutRatio));
            int rooms = prevWave.rooms;
            int drinks = prevWave.drinks;
            int food = prevWave.food;
            int relax = prevWave.relax;
            int totalNeeds = rooms + drinks + food + relax + oceanViewCount;
            int guests = Mathf.Max(1, Mathf.RoundToInt(totalNeeds / 2.0f));

            if (logDetails)
            {
                Debug.Log($"<color=yellow>[MÄKKÝ DEBUT BUNGALOVY - Vlna {waveIndex + 1}]</color> Ocean View: {oceanViewCount} ks. Ostatné zmrazené.");
            }

            return new WaveDefinition(guests, rooms, drinks, food, relax, oceanViewCount);
        }

        return baseWave;
    }

    public WaveDefinition GetUpcomingWave()
    {
        if (Session == null || catalog == null) return null;

        int index = useTestWave ? testWaveNumber - 1 : Session.WaveNumber;
        if (index < 0 || index >= catalog.Count) return null;

        HashSet<NeedType> availableNeeds = GetBuiltBuildingNeeds();
        return CalculateAdaptedWave(index, availableNeeds, logDetails: false);
    }

    private void OnWaveFinishedInternal(int waveNumber)
    {
        HashSet<NeedType> availableNeeds = GetBuiltBuildingNeeds();
        if (availableNeeds.Contains(NeedType.Relax)) hasRelaxDebuted = true;
        if (availableNeeds.Contains(NeedType.OceanView)) hasOceanViewDebuted = true;

        NotifyWaveEnded(waveNumber);
    }

    private void SyncDebutsForTestWave(int waveNum)
    {
        if (testLatePoolDebut)
        {
            hasRelaxDebuted = false;
            hasOceanViewDebuted = false;
            return;
        }

        if (waveNum <= 3)
        {
            hasRelaxDebuted = false;
            hasOceanViewDebuted = false;
        }
        else if (waveNum >= 4 && waveNum <= 6)
        {
            hasRelaxDebuted = (waveNum > 4);
            hasOceanViewDebuted = false;
        }
        else if (waveNum >= 7)
        {
            hasRelaxDebuted = true;
            hasOceanViewDebuted = (waveNum > 7);
        }
    }

    public HashSet<NeedType> GetBuiltBuildingNeeds()
    {
        HashSet<NeedType> availableNeeds = new HashSet<NeedType>();
        BuildingInfo[] allBuildings = FindObjectsOfType<BuildingInfo>();

        foreach (BuildingInfo building in allBuildings)
        {
            if (building != null && building.gameObject.activeInHierarchy && building.isBuilt)
            {
                if (building.offeredNeed != NeedType.None)
                {
                    availableNeeds.Add(building.offeredNeed);
                }
            }
        }

        return availableNeeds;
    }

    [ContextMenu("Reset Wave to Waiting")]
    public void ResetWave()
    {
        if (Session != null && Session.State != WaveState.Running)
        {
            Session.SetWaveIndex(testWaveNumber - 1);
            hasRelaxDebuted = false;
            hasOceanViewDebuted = false;
            lastExecutedWave = null;
            Debug.Log("<color=yellow>Vlna bola resetovaná do stavu Waiting.</color>");
        }
    }

    public bool ResolveNeed(int id) => Session != null && Session.ResolveNeed(id);
    public bool BeginService(int id) => Session != null && Session.BeginService(id);

    private void NotifyWaveEnded(int waveNumber) => onWaveEnded.Invoke(waveNumber);

    private void OnDestroy()
    {
        if (Session != null) Session.WaveEnded -= OnWaveFinishedInternal;
    }
}