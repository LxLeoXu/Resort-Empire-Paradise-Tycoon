using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LayoutElement))]
public class NeedCardUI : MonoBehaviour
{
    [Header("Identifikácia")]
    public NeedType needType;

    [Header("UI Prvky")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image workerIndicator;

    [Header("Varovanie (Bottleneck)")]
    [SerializeField] private GameObject warningBox;
    [SerializeField] private TextMeshProUGUI warningText;

    [Header("Dynamická výška karty")]
    [SerializeField] private float normalHeight = 100f;
    [SerializeField] private float warningHeight = 160f;

    [Header("Farby stavov")]
    [SerializeField] private Color normalColor = Color.black;
    [SerializeField] private Color bottleneckColor = new Color(0.9f, 0.2f, 0.2f);

    private Button cardButton;
    private LayoutElement layoutElement;

    private void Awake()
    {
        cardButton = GetComponent<Button>();
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }

        layoutElement = GetComponent<LayoutElement>();
        SetCardHeight(normalHeight);
    }

    public void Setup(NeedType type, string displayName, Sprite icon)
    {
        needType = type;
        if (titleText != null) titleText.text = displayName;
        if (iconImage != null && icon != null) iconImage.sprite = icon;

        SetWarning(false, string.Empty);
        UpdateProgress(0, 0);
        SetWorkerPresent(false);
    }

    public void UpdateProgress(int served, int total)
    {
        if (countText != null)
        {
            countText.text = $"{served}/{total}";
        }

        if (progressSlider != null)
        {
            progressSlider.maxValue = total > 0 ? total : 1;
            progressSlider.value = served;
        }
    }

    public void SetWarning(bool isBottleneck, string message = "")
    {
        if (warningBox != null)
            warningBox.SetActive(isBottleneck);

        if (warningText != null && isBottleneck)
            warningText.text = message;

        if (countText != null)
            countText.color = isBottleneck ? bottleneckColor : normalColor;

        SetCardHeight(isBottleneck ? warningHeight : normalHeight);
    }

    private void SetCardHeight(float height)
    {
        if (layoutElement != null)
        {
            layoutElement.preferredHeight = height;
            layoutElement.minHeight = height;
        }
    }

    public void SetWorkerPresent(bool hasWorker)
    {
        if (workerIndicator != null)
            workerIndicator.gameObject.SetActive(hasWorker);
    }

    private void OnCardClicked()
    {
        WorkerAssignUI ui = FindFirstObjectByType<WorkerAssignUI>(FindObjectsInactive.Include);
        if (ui != null)
            ui.OpenForNeed(needType);
        else
            Debug.LogError("Nepodarilo sa nájsť WorkerAssignUI (S3) v scéne!");
    }
}