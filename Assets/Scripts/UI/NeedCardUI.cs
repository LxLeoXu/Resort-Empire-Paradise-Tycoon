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

    [Header("Speed Up")]
    [Tooltip("Tlačidlo Speed Up ⚡3. Ukáže sa len pri preťaženej budove, ktorá ešte nemala zrýchlenie v tejto vlne.")]
    [SerializeField] private Button speedUpButton;
    [Tooltip("Hláška pri < 3 energiách (nesmie byť dieťaťom tlačidla). Vypína sa sama.")]
    [SerializeField] private TextMeshProUGUI speedUpHint;
    [SerializeField] private string speedUpHintMessage = "Not enough energy.";
    [SerializeField] private float speedUpHintSeconds = 2.5f;

    private Button cardButton;
    private LayoutElement layoutElement;
    private BuildingService service;
    private bool warning;
    private float hintUntil;

    private void Awake()
    {
        cardButton = GetComponent<Button>();
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }

        layoutElement = GetComponent<LayoutElement>();
        SetCardHeight(normalHeight);

        if (speedUpButton != null)
        {
            speedUpButton.onClick.AddListener(OnSpeedUpClicked);
            speedUpButton.gameObject.SetActive(false);
        }
        if (speedUpHint != null)
        {
            speedUpHint.text = speedUpHintMessage;
            speedUpHint.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (speedUpButton != null)
        {
            BuildingService s = warning ? FindService() : null;
            bool show = s != null && s.CanBoost;
            if (speedUpButton.gameObject.activeSelf != show) speedUpButton.gameObject.SetActive(show);
        }
        if (speedUpHint != null)
        {
            bool showHint = Time.unscaledTime < hintUntil;
            if (speedUpHint.gameObject.activeSelf != showHint) speedUpHint.gameObject.SetActive(showHint);
        }
    }

    private BuildingService FindService()
    {
        if (service != null) return service;
        foreach (BuildingService s in FindObjectsByType<BuildingService>(FindObjectsSortMode.None))
        {
            if (s.Need != needType) continue;
            service = s;
            break;
        }
        return service;
    }

    private void OnSpeedUpClicked()
    {
        BuildingService s = FindService();
        if (s == null || !s.CanBoost) return;

        ResourceManager resources = ResourceManager.Instance;
        if (resources == null || !resources.HasEnough(ResourceType.Energy, BuildingService.BoostEnergyCost))
        {
            hintUntil = Time.unscaledTime + speedUpHintSeconds;   // tap costs nothing, just explains where energy comes from
            return;
        }
        s.TryBoost();
    }

    public void Setup(NeedType type, string displayName, Sprite icon)
    {
        needType = type;
        service = null;
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
        warning = isBottleneck;

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