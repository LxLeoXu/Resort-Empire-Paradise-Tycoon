using UnityEngine.Localization.Settings;
using UnityEngine;

public class WaveS4Connector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private S4FigmaResultsUI resultsUI;

    [Header("Wave Feedback")]
[SerializeField, Min(0f)]
private float queueDelayThresholdSeconds = 8f;

    [Header("Temporary Testing Only")]
    [SerializeField] private bool useTestSatisfaction = false;

    [SerializeField, Range(0, 100)]
    private int testSatisfaction = 88;

    private WaveSession session;
    private int? completedWaveId;
    private int? actualSatisfaction;

    private bool resultsDisplayed;
    public float QueueDelayThresholdSeconds => queueDelayThresholdSeconds;

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

        // SCRUM-474: Pocet obsluzenych VIP hosti
        int vipServed = session.VipServedCount;

        Debug.Log(
            $"[VIP REWARDS] Wave {waveId}: " +
            $"{vipServed} VIP hostia = +{vipServed} Materials"
);

// SCRUM-474: Pocet uspesne obsluzenych VIP hosti


BuildFeedback(session, queueDelayThresholdSeconds, out string positiveFeedback, out string negativeFeedback);

resultsDisplayed = resultsUI.ShowWaveResults(
    waveId,
    completedNeeds,
    totalNeeds,
    satisfaction,
    positiveFeedback: positiveFeedback,
    negativeFeedback: negativeFeedback,
    vipServed: vipServed
);

        if (resultsDisplayed)
{
    Debug.Log(
        $"S4 opened: Wave {waveId}, " +
        $"Needs {completedNeeds}/{totalNeeds}, " +
        $"Satisfaction {satisfaction}%");

    Debug.Log(
        $"[WAVE FEEDBACK] Positive: {positiveFeedback}");

    Debug.Log(
        $"[WAVE FEEDBACK] Improvement: {negativeFeedback}");
}
    }

    private void OnDestroy()
    {
        if (session != null)
            session.WaveStarted -= OnWaveStarted;

        if (waveManager != null)
            waveManager.OnWaveEnded.RemoveListener(OnWaveEnded);
    }
    public static void BuildFeedback(WaveSession source, float threshold, out string positive, out string negative)
    {
        NeedType best = source.GetBestServedNeedType();
        NeedType delayed = source.GetMostDelayedNeed(threshold);
        positive = best == NeedType.None
            ? GetFeedbackText("WaveFeedback_NoData", "Not enough data yet")
            : GetFeedbackText("WaveFeedback_Positive", "{0} went great!", GetNeedDisplayName(best));
        negative = delayed == NeedType.None
            ? GetFeedbackText("WaveFeedback_Smooth", "Everything ran smoothly")
            : GetFeedbackText("WaveFeedback_Improvement", "{0} capacity needs improvement", GetBuildingDisplayName(delayed));
    }

    // SCRUM-431: Nazov najlepsie obsluzenej potreby.
private static string GetNeedDisplayName(NeedType type)
{
    return type switch
    {
        NeedType.Rooms => "Rooms",
        NeedType.Drinks => "Drinks",
        NeedType.Food => "Food",
        NeedType.Relax => "Relax",
        NeedType.OceanView => "Ocean View",
        _ => "Unknown"
    };
}

// SCRUM-395: Nazov budovy s najdlhsim cakanim.
private static string GetBuildingDisplayName(NeedType type)
{
    return type switch
    {
        NeedType.Rooms => "Reception",
        NeedType.Drinks => "Beach Bar",
        NeedType.Food => "Restaurant",
        NeedType.Relax => "Pool",
        NeedType.OceanView => "Bungalows",
        _ => "Building"
    };
}

// SCRUM-467: Nacitanie textu z GameTexts.
private static string GetFeedbackText(
    string key,
    string fallback,
    params object[] args)
{
    try
    {
        string localized =
            LocalizationSettings.StringDatabase
                .GetLocalizedString("GameTexts", key, args);

        if (!string.IsNullOrWhiteSpace(localized))
            return localized;
    }
    catch (System.Exception exception)
    {
        Debug.LogWarning(
            $"Wave Feedback localization: {exception.Message}");
    }

    return string.Format(fallback, args);
}
}
