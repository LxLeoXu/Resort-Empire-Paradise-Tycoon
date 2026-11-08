using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorkerCardUI : MonoBehaviour
{
    [Header("Karta")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image[] starImages;
    [SerializeField] private Color activeStarColor = new Color(1f, 0.75f, 0.10f);
    [SerializeField] private Color inactiveStarColor = new Color(0.62f, 0.65f, 0.68f);

    [Header("Stav (badge)")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image statusBackground;
    [SerializeField] private Color availableColor = new Color(0.1f, 0.7f, 0.5f);
    [SerializeField] private Color assignedColor = new Color(1f, 0.5f, 0.2f);
    [SerializeField] private Color busyColor = new Color(0.9f, 0.75f, 0.4f);

    [Header("Buttony")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Button unselectButton;

    [Header("Zbledla karta (pracovnik je inde)")]
    [Range(0f, 1f)] [SerializeField] private float busyAlpha = 0.55f;

    private WorkerRoster.Worker worker;
    private BuildingService building;

    private void Awake()
    {
        if (selectButton != null) selectButton.onClick.AddListener(OnButtonClicked);
        if (unselectButton != null) unselectButton.onClick.AddListener(OnButtonClicked);
    }

    public void Bind(WorkerRoster.Worker newWorker, BuildingService newBuilding)
    {
        worker = newWorker;
        building = newBuilding;
        Refresh();
    }

    public void Refresh()
    {
        if (worker == null) return;

        bool here = worker.building == building;
        bool busy = !worker.IsFree && !here;

        if (nameText != null) nameText.text = worker.displayName;

        for (int i = 0; i < starImages.Length; i++)
            if (starImages[i] != null)
                starImages[i].color = i < worker.stars ? activeStarColor : inactiveStarColor;

        if (statusText != null)
            statusText.text = busy ? "BUSY" : here ? "ASSIGNED" : "AVAILABLE";
        if (statusBackground != null)
            statusBackground.color = busy ? busyColor : here ? assignedColor : availableColor;

        if (selectButton != null) selectButton.gameObject.SetActive(!busy && !here);
        if (unselectButton != null) unselectButton.gameObject.SetActive(here);

        if (group != null)
        {
            group.alpha = busy ? busyAlpha : 1f;
            group.interactable = !busy;
        }
    }

    private void OnButtonClicked()
    {
        if (worker == null || building == null) return;
        WorkerRoster.Instance.Toggle(worker, building);
    }
}