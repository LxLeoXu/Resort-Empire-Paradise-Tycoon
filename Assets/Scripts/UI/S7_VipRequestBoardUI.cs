using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class S7_VipRequestBoardUI : MonoBehaviour
{
    private const string TableName = "GameTexts";

    private static readonly Color ColorReadySummary = new Color(0.086f, 0.396f, 0.204f); // #166534
    private static readonly Color ColorError = new Color(0.725f, 0.11f, 0.11f);         // #B91C1C
    private static readonly Color ColorServeEnabled = new Color(0.271f, 0.102f, 0.012f);// #451A03
    private static readonly Color ColorServeDisabled = new Color(0.49f, 0.447f, 0.588f); // #7D7296

    [Header("Ovládanie okna")]
    [SerializeField] private Button closeButton;

    [Header("Ľavý panel (dynamické prvky)")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text guestNameText;
    [SerializeField] private Image[] miniStatusIcons = new Image[4];
    [SerializeField] private TMP_Text servingAmountText;

    [Header("Riadky požiadaviek")]
    [SerializeField] private S7_VIPRowUI[] rows = new S7_VIPRowUI[4];

    [Header("Spodné tlačidlo Serve All")]
    [SerializeField] private Button serveAllButton;
    [SerializeField] private Image serveAllIcon;
    [SerializeField] private TMP_Text serveAllText;
    [SerializeField] private Sprite serveEnabledIconSprite;
    [SerializeField] private Sprite serveDisabledIconSprite;

    [Header("Predvolené ikony potrieb")]
    [SerializeField] private Sprite roomsIcon;
    [SerializeField] private Sprite drinksIcon;
    [SerializeField] private Sprite foodIcon;
    [SerializeField] private Sprite relaxIcon;
    [SerializeField] private Sprite oceanViewIcon;

    private VipGuestController controller;
    private float errorMessageUntil = -1f;
    private string errorMessage = "";

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (serveAllButton != null)
            serveAllButton.onClick.AddListener(OnServeAllClicked);
    }

    private void OnEnable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
    }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.Changed -= Refresh;
            controller.VipCompleted -= OnVipCompleted;
        }
    }

    public void Show(VipGuestController vipController)
    {
        if (vipController == null || vipController.Definition == null)
            return;

        if (controller != null)
        {
            controller.Changed -= Refresh;
            controller.VipCompleted -= OnVipCompleted;
        }

        controller = vipController;
        controller.Changed += Refresh;
        controller.VipCompleted += OnVipCompleted;

        errorMessageUntil = -1f;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf || controller == null || controller.Definition == null)
            return;

        var reqs = controller.Definition.requirements;
        for (int i = 0; i < rows.Length && i < reqs.Count; i++)
        {
            if (rows[i] == null) continue;
            var req = reqs[i];
            if (controller.GetRequirementState(req) == VipRequirementState.InProgress)
            {
                rows[i].UpdateProgressOnly(
                    controller.GetRemainingSeconds(req.id),
                    controller.GetTotalDuration(req.id));
            }
        }

        if (errorMessageUntil > 0f && Time.unscaledTime >= errorMessageUntil)
        {
            errorMessageUntil = -1f;
            UpdateSummaryText(controller.GetReadyCount());
        }
    }

    private void OnResourceChanged(ResourceType type, int newAmount)
    {
        if (type == ResourceType.Materials && gameObject.activeSelf)
            Refresh();
    }

    private void OnVipCompleted(int completed, int total, int satisfaction, List<VipReward> rewards)
    {
        Close();
    }

    public void Refresh()
    {
        if (controller == null || controller.Definition == null)
            return;

        var def = controller.Definition;

        if (portraitImage != null && def.portrait != null)
            portraitImage.sprite = def.portrait;

        if (guestNameText != null)
            guestNameText.text = def.displayName;

        int playerMaterials = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetResource(ResourceType.Materials)
            : 0;

        int readyCount = 0;
        var reqs = def.requirements;

        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null) continue;

            if (reqs == null || i >= reqs.Count)
            {
                rows[i].gameObject.SetActive(false);
                if (i < miniStatusIcons.Length && miniStatusIcons[i] != null)
                    miniStatusIcons[i].gameObject.SetActive(false);
                continue;
            }

            var req = reqs[i];
            var state = controller.GetRequirementState(req);
            if (state == VipRequirementState.Ready) readyCount++;

            Sprite icon = req.icon != null ? req.icon : GetNeedSprite(req.needType);

            rows[i].Bind(
                req,
                state,
                icon,
                playerMaterials,
                controller.GetRemainingSeconds(req.id),
                controller.GetTotalDuration(req.id),
                HandleFixClicked);

            if (i < miniStatusIcons.Length && miniStatusIcons[i] != null)
            {
                miniStatusIcons[i].gameObject.SetActive(true);
                miniStatusIcons[i].sprite = rows[i].GetStatusSprite(state);
            }
        }

        UpdateSummaryText(readyCount);
        UpdateServeAllButton(readyCount > 0);
    }

    private void HandleFixClicked(VipRequirement req)
    {
        if (controller == null || req == null) return;

        int have = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetResource(ResourceType.Materials)
            : 0;

        if (have < req.materials)
        {
            errorMessage = GetLocalized("vip_not_enough_materials", $"Need {req.materials} Materials (have {have})", have, req.materials);
            errorMessageUntil = Time.unscaledTime + 2.5f;
            UpdateSummaryText(controller.GetReadyCount());
            return;
        }

        if (!controller.TryFulfillRequirement(req.id))
            Debug.LogWarning($"S7: Fix zlyhal: {controller.LastError}");
    }

    private void OnServeAllClicked()
    {
        if (controller == null) return;
        controller.StartServeAll();
    }

    private void UpdateSummaryText(int readyCount)
    {
        if (servingAmountText == null) return;

        if (errorMessageUntil > 0f)
        {
            servingAmountText.text = errorMessage;
            servingAmountText.color = ColorError;
        }
        else
        {
            servingAmountText.text = GetLocalized("vip_ready_to_serve", $"{readyCount} ready to serve", readyCount);
            servingAmountText.color = ColorReadySummary;
        }
    }

    private void UpdateServeAllButton(bool canServe)
    {
        if (serveAllButton != null)
            serveAllButton.interactable = canServe;

        if (serveAllIcon != null)
        {
            Sprite spr = canServe ? serveEnabledIconSprite : serveDisabledIconSprite;
            if (spr != null) serveAllIcon.sprite = spr;
        }

        if (serveAllText != null)
            serveAllText.color = canServe ? ColorServeEnabled : ColorServeDisabled;
    }

    private Sprite GetNeedSprite(NeedType type) => type switch
    {
        NeedType.Rooms => roomsIcon,
        NeedType.Drinks => drinksIcon,
        NeedType.Food => foodIcon,
        NeedType.Relax => relaxIcon,
        NeedType.OceanView => oceanViewIcon,
        _ => drinksIcon
    };

    private static string GetLocalized(string key, string fallback, params object[] args)
    {
        try
        {
            string s = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key, args);
            return string.IsNullOrEmpty(s) ? string.Format(fallback, args) : s;
        }
        catch { return string.Format(fallback, args); }
    }
}