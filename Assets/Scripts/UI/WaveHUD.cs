using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

public sealed class WaveHUD : MonoBehaviour
{
    [SerializeField] private WaveManager manager;
    [SerializeField] private UnityEngine.UI.Button startWave;
    [SerializeField] private TMP_Text startLabel;
    [SerializeField] private TMP_Text statusLabel;
    [SerializeField] private LocalizeStringEvent startLocalization;
    [SerializeField] private LocalizeStringEvent statusLocalization;

    private WaveSession session;

    private void OnEnable()
    {
        // Start runs after all Awake calls, including WaveManager's initialization.
        if (session != null) Bind();
    }

    private void Start()
    {
        if (manager == null || manager.Session == null || startWave == null || startLabel == null || statusLabel == null
            || startLocalization == null || statusLocalization == null)
        {
            Debug.LogError("WaveHUD: Missing wave manager, button or labels.", this);
            enabled = false;
            return;
        }

        session = manager.Session;
        Bind();
    }

    private void Bind()
    {
        session.StateChanged += Refresh;
        startWave.onClick.AddListener(manager.StartWave);
        Refresh();
    }

    private void OnDisable()
    {
        if (session == null) return;
        session.StateChanged -= Refresh;
        startWave.onClick.RemoveListener(manager.StartWave);
    }

    private void Refresh()
    {
        startWave.interactable = session.CanStartWave;
        startLocalization.StringReference.SetReference("GameTexts", "welcome_guests");
        statusLocalization.StringReference.SetReference("GameTexts", session.State == WaveState.Running
            ? "wave_in_progress" : session.CanStartWave ? "ready_for_guests" : "all_waves_complete");
        startLocalization.RefreshString();
        statusLocalization.RefreshString();
    }
}
