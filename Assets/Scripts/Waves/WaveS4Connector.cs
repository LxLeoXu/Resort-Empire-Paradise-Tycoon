
using UnityEngine;

public class WaveS4Connector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private S4FigmaResultsUI resultsUI;

    [Header("Temporary Testing Only")]
    [SerializeField] private bool useTestSatisfaction = false;

    [SerializeField, Range(0, 100)]
    private int testSatisfaction = 88;

    private WaveSession session;
    private int? completedWaveId;
    private int? actualSatisfaction;

    private bool resultsDisplayed;

    private void Start()
    {
        if (waveManager == null || resultsUI == null)
        {
            Debug.LogError(
                "WaveS4Connector: Missing references!", this);
            enabled = false;
            return;
        }

        session = waveManager.Session;

        if (session == null)
        {
            Debug.LogError(
                "WaveS4Connector: WaveSession is missing!", this);
            enabled = false;
            return;
        }

        session.WaveStarted += OnWaveStarted;
        waveManager.OnWaveEnded.AddListener(OnWaveEnded);
    }

    private void OnWaveStarted(int waveId)
    {
        completedWaveId = null;
        actualSatisfaction = null;
        resultsDisplayed = false;
    }

    // Zavola buduci system spokojnosti.
    public void SetSatisfactionPercent(int percent)
    {
        actualSatisfaction = Mathf.Clamp(percent, 0, 100);

        TryShowResults();
    }

    private void OnWaveEnded(int waveId)
    {
        completedWaveId = waveId;

        Debug.Log("Wave ended: " + waveId);

        TryShowResults();
    }

    private void TryShowResults()
    {
        if (resultsDisplayed ||
            session == null ||
            !completedWaveId.HasValue)
            return;

        int waveId = completedWaveId.Value;

        if (session.State != WaveState.Result ||
            session.WaveNumber != waveId)
            return;

        if (!actualSatisfaction.HasValue &&
            !useTestSatisfaction)
        {
            Debug.LogWarning(
                "S4: Wave ended, but satisfaction " +
                "data is not available yet.");
            return;
        }

        int satisfaction = actualSatisfaction
            ?? testSatisfaction;

        int completedNeeds = session.ServedCount;
        int totalNeeds = session.EmittedCount;

        resultsDisplayed = resultsUI.ShowWaveResults(
            waveId,
            completedNeeds,
            totalNeeds,
            satisfaction
        );

        if (resultsDisplayed)
        {
            Debug.Log(
                $"S4 opened: Wave {waveId}, " +
                $"Needs {completedNeeds}/{totalNeeds}, " +
                $"Satisfaction {satisfaction}%");
        }
    }

    private void OnDestroy()
    {
        if (session != null)
            session.WaveStarted -= OnWaveStarted;

        if (waveManager != null)
            waveManager.OnWaveEnded.RemoveListener(OnWaveEnded);
    }
}
