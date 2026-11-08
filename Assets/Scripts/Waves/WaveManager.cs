using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public sealed class WaveManager : MonoBehaviour
{
    [SerializeField] private WaveCatalog catalog;
    [Min(0.01f)][SerializeField] private float arrivalDuration = 90f;
    [Tooltip("SCRUM-235: connect S4 to this event. Argument: completed wave number (1-based).")]
    [SerializeField] private UnityEvent<int> onWaveEnded = new();

    [Header("SCRUM-25 Testovanie")]
    [Tooltip("Zaškrtni, ak chceš manuálne testovať konkrétnu vlnu.")]
    [SerializeField] private bool useTestWave = true;
    [Tooltip("Nastav na 4 pre testovanie Bazéna / Relax.")]
    [Range(1, 8)][SerializeField] private int testWaveNumber = 4;

    public WaveSession Session { get; private set; }
    public GuestWavePresentation Guests { get; private set; }
    public UnityEvent<int> OnWaveEnded => onWaveEnded;

    private void Awake()
    {
        if (catalog == null)
        {
            Debug.LogError("WaveManager: Assign a Wave Catalog in the Inspector.", this);
            enabled = false;
            return;
        }
        Session = new WaveSession(catalog);
        Session.WaveEnded += NotifyWaveEnded;
        Guests = GetComponent<GuestWavePresentation>();
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

        // Ak testujeme, nastavíme vlnu na požadovanú (testWaveNumber 4 = index 3 v katalógu)
        if (useTestWave)
        {
            Session.SetWaveIndex(testWaveNumber - 1);
        }

        HashSet<NeedType> availableNeeds = GetBuiltBuildingNeeds();
        Session.StartWave(arrivalDuration, availableNeeds);
    }

    private HashSet<NeedType> GetBuiltBuildingNeeds()
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

    /// <summary>
    /// Umožní reštartovať vlnu priamo z Unity editora kliknutím pravým tlačidlom na komponent
    /// </summary>
    [ContextMenu("Reset Wave to Waiting")]
    public void ResetWave()
    {
        if (Session != null && Session.State != WaveState.Running)
        {
            Session.SetWaveIndex(testWaveNumber - 1);
            Debug.Log("<color=yellow>Vlna bola resetovaná do stavu Waiting.</color>");
        }
    }

    public bool ResolveNeed(int id) => Session != null && Session.ResolveNeed(id);
    public bool BeginService(int id) => Session != null && Session.BeginService(id);

    private void NotifyWaveEnded(int waveNumber) => onWaveEnded.Invoke(waveNumber);

    private void OnDestroy()
    {
        if (Session != null) Session.WaveEnded -= NotifyWaveEnded;
    }
}
