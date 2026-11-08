using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class NextWavePanel : MonoBehaviour
{
    [Header("Referencie")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Obnovovanie")]
    [Min(0.1f)] [SerializeField] private float refreshInterval = 0.5f;

    private static readonly NeedType[] Order =
        { NeedType.Rooms, NeedType.Drinks, NeedType.Food, NeedType.Relax, NeedType.OceanView };

    private CanvasGroup canvasGroup;
    private WaveSession session;
    private string lastText;
    private float timer;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (waveManager == null)
            waveManager = FindFirstObjectByType<WaveManager>();

        SetVisible(false);
    }

    private void Start()
    {
        if (waveManager == null || waveManager.Session == null)
        {
            Debug.LogError("NextWavePanel: WaveManager not found.", this);
            enabled = false;
            return;
        }

        session = waveManager.Session;
        session.StateChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (session != null)
            session.StateChanged -= Refresh;
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < refreshInterval) return;
        timer = 0f;
        Refresh();
    }

    private void Refresh()
    {
        if (session == null) return;

        if (session.State == WaveState.Running)
        {
            SetVisible(false);
            return;
        }

        WaveDefinition wave = waveManager.GetUpcomingWave();
        if (wave == null)
        {
            SetVisible(false);
            return;
        }

        HashSet<NeedType> built = waveManager.GetBuiltBuildingNeeds();
        StringBuilder sb = new StringBuilder();

        foreach (NeedType type in Order)
        {
            int count = wave.GetCount(type);
            if (count <= 0 || !built.Contains(type)) continue;

            if (sb.Length > 0) sb.Append(" · ");
            sb.Append(GetName(type)).Append(' ').Append(count);
        }

        if (sb.Length == 0)
        {
            SetVisible(false);
            return;
        }

        string text = "Next wave: " + sb;
        if (infoText != null && text != lastText)
        {
            infoText.text = text;
            lastText = text;
        }

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;
    }

    private static string GetName(NeedType type) => type switch
    {
        NeedType.Rooms => "Rooms",
        NeedType.Drinks => "Drinks",
        NeedType.Food => "Food",
        NeedType.Relax => "Relax",
        NeedType.OceanView => "Ocean View",
        _ => type.ToString()
    };
}