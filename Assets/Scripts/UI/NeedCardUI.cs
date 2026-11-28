using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Settings;

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

    [Header("Dobieha (RECOVERING)")]
    [Tooltip("Rodičovský objekt RECOVERING boxu (ikona + text). Vypnutý, kým budova nedobieha.")]
    [SerializeField] private GameObject recoveringBox;
    [Tooltip("Image s ikonou Status_CatchingUp.png (voliteľné, môže byť nastavená priamo v prefabe).")]
    [SerializeField] private Image recoveringIcon;
    [SerializeField] private TextMeshProUGUI recoveringText;
    [SerializeField] private Color recoveringColor = new Color(0.95f, 0.65f, 0.1f);

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
    private static readonly Color SatisfiedColor = new Color32(21, 121, 74, 255);
    private static readonly Color FullColor = new Color32(149, 83, 16, 255);
    private Image warningIcon;
    private float hintUntil;

    private void Awake()
    {
        if (warningBox != null)
            warningIcon = warningBox.transform.Find("Icon")?.GetComponent<Image>();
        cardButton = GetComponent<Button>();
        if (cardButton != null)
        {
            cardButton.onClick.AddListener(OnCardClicked);
        }

        layoutElement = GetComponent<LayoutElement>();
        SetCardHeight(normalHeight);

        if (recoveringBox != null)
            recoveringBox.SetActive(false);

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

        ShowStatus(NeedCardStatus.Ok, string.Empty);
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

    public void ShowStatus(NeedCardStatus status, string buildingName)
    {
        bool full = status == NeedCardStatus.Full;
        bool satisfied = status == NeedCardStatus.Satisfied;
        bool recovering = status == NeedCardStatus.Recovering;
        warning = full || status == NeedCardStatus.NoWorker || status == NeedCardStatus.Bottleneck;
        Color color = status switch
        {
            NeedCardStatus.Full => FullColor,
            NeedCardStatus.Satisfied => SatisfiedColor,
            NeedCardStatus.Recovering => recoveringColor,
            NeedCardStatus.Ok => normalColor,
            _ => bottleneckColor
        };
        if (warningBox != null) warningBox.SetActive(warning);
        if (recoveringBox != null) recoveringBox.SetActive(recovering || satisfied);
        if (warningIcon != null) warningIcon.gameObject.SetActive(!full);
        if (recoveringIcon != null) recoveringIcon.gameObject.SetActive(recovering);
        if (countText != null) countText.color = color;

        if (warning && warningText != null)
        {
            warningText.color = color;
            warningText.text = status switch
            {
                NeedCardStatus.Full => Localize("s2_full"),
                NeedCardStatus.NoWorker => Localize("s2_no_worker"),
                _ => $"{buildingName} Waiting Too Long"
            };
        }
        if ((recovering || satisfied) && recoveringText != null)
        {
            recoveringText.color = color;
            recoveringText.text = satisfied ? Localize("s2_satisfied") : $"{buildingName} is catching up";
        }
        SetCardHeight(status == NeedCardStatus.Ok ? normalHeight : warningHeight);
    }

    private static string Localize(string key)
        => LocalizationSettings.StringDatabase.GetLocalizedString("GameTexts", key);

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