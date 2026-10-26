using UnityEngine;
using UnityEngine.Events;

public sealed class WaveManager : MonoBehaviour
{
    [SerializeField] private WaveCatalog catalog;
    [Min(0.01f)] [SerializeField] private float arrivalDuration = 90f;
    [Tooltip("SCRUM-235: connect S4 to this event. Argument: completed wave number (1-based).")]
    [SerializeField] private UnityEvent<int> onWaveEnded = new();

    public WaveSession Session { get; private set; }
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
    }

    private void Update() => Session?.Tick(Time.deltaTime);

    public void StartWave()
    {
        Session?.StartWave(arrivalDuration);
    }

    public bool ResolveNeed(int id) => Session != null && Session.ResolveNeed(id);
    public bool BeginService(int id) => Session != null && Session.BeginService(id);

    private void NotifyWaveEnded(int waveNumber) => onWaveEnded.Invoke(waveNumber);

    private void OnDestroy()
    {
        if (Session != null) Session.WaveEnded -= NotifyWaveEnded;
    }
}
