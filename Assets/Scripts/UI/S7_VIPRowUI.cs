using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class S7_VIPRowUI : MonoBehaviour
{
    private const string TableName = "GameTexts";

    private static readonly Color ColorMissing = new Color(0.725f, 0.11f, 0.11f);     // #B91C1C
    private static readonly Color ColorReady = new Color(0.086f, 0.396f, 0.204f);     // #166534
    private static readonly Color ColorInProgress = new Color(0.012f, 0.412f, 0.631f);// #0369A1
    private static readonly Color ColorCompleted = new Color(0.573f, 0.251f, 0.055f); // #92400E
    private static readonly Color ColorNormalText = new Color(0.271f, 0.102f, 0.012f);// #451A03

    [Header("Dynamický obsah riadku")]
    [SerializeField] private Image mainIcon;
    [SerializeField] private TMP_Text requestNameText;
    [SerializeField] private TMP_Text locationNameText;
    [SerializeField] private GameObject bonusBadge;
    [Tooltip("Voliteľné: CanvasGroup na ľavej časti pre stlmenie na 0.55 pri DONE.")]
    [SerializeField] private CanvasGroup leftContentCanvasGroup;

    [Header("Strop kvality (MissingRequirement z hierarchie)")]
    [Tooltip("Sem pretiahni celý rodičovský objekt MissingRequirement (zapne sa len keď rola chýba).")]
    [SerializeField] private GameObject missingRequirementRoot;
    [Tooltip("Sem pretiahni vnútorný Text (zobrazí napr. 'No Butler - max 3').")]
    [SerializeField] private TMP_Text roleCapText;
    [Tooltip("Voliteľné: vnútorný objekt StarIcon (ak ho máš oddelene ako Image).")]
    [SerializeField] private GameObject starIconObject;
    [SerializeField] private Color roleCapColor = new Color(0.9f, 0.25f, 0.25f);

    [Header("Stavový štítok (State)")]
    [SerializeField] private Image stateBackground;
    [SerializeField] private Image stateIcon;
    [SerializeField] private TMP_Text stateText;

    [Header("Akcie vpravo")]
    [SerializeField] private Button fixButton;
    [SerializeField] private TMP_Text fixAmountText;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text timerText;

    [Header("Sprity stavov (4x Chip + 4x Status)")]
    [SerializeField] private Sprite chipMissing;
    [SerializeField] private Sprite chipReady;
    [SerializeField] private Sprite chipInProgress;
    [SerializeField] private Sprite chipCompleted;
    [SerializeField] private Sprite statusMissing;
    [SerializeField] private Sprite statusReady;
    [SerializeField] private Sprite statusInProgress;
    [SerializeField] private Sprite statusCompleted;

    private VipRequirement currentReq;
    private Action<VipRequirement> onFixClicked;

    private void Awake()
    {
        if (fixButton != null)
            fixButton.onClick.AddListener(() => onFixClicked?.Invoke(currentReq));
    }

    public Sprite GetStatusSprite(VipRequirementState state) => state switch
    {
        VipRequirementState.Missing => statusMissing,
        VipRequirementState.Ready => statusReady,
        VipRequirementState.InProgress => statusInProgress,
        VipRequirementState.Completed => statusCompleted,
        _ => statusMissing
    };

    public void Bind(
        VipRequirement req,
        VipRequirementState state,
        Sprite needIconSprite,
        int playerMaterials,
        float remainingSec,
        float totalDurationSec,
        Action<VipRequirement> fixCallback)
    {
        Bind(req, state, needIconSprite, playerMaterials, remainingSec, totalDurationSec, false, 5, fixCallback);
    }

    public void Bind(
        VipRequirement req,
        VipRequirementState state,
        Sprite needIconSprite,
        int playerMaterials,
        float remainingSec,
        float totalDurationSec,
        bool isRoleMissing,
        int starCap,
        Action<VipRequirement> fixCallback)
    {
        currentReq = req;
        onFixClicked = fixCallback;

        if (req == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (mainIcon != null && needIconSprite != null)
            mainIcon.sprite = needIconSprite;

        if (requestNameText != null)
            requestNameText.text = req.description;

        if (locationNameText != null)
            locationNameText.text = string.IsNullOrWhiteSpace(req.locationName)
                ? GetDefaultLocationName(req.needType)
                : req.locationName;

        if (bonusBadge != null)
            bonusBadge.SetActive(req.bonus);

        // --- Zobrazenie stropu (MissingRequirement) ---
        bool showCap = isRoleMissing && req.requiredRole != VipRequiredRole.None && starCap < 5;

        if (missingRequirementRoot != null)
            missingRequirementRoot.SetActive(showCap);

        if (starIconObject != null)
            starIconObject.SetActive(showCap);

        if (roleCapText != null)
        {
            if (missingRequirementRoot == null)
                roleCapText.gameObject.SetActive(showCap);

            if (showCap)
            {
                string roleName = WorkerRoster.GetRoleDisplayName(req.requiredRole);
                // Ak máš vedľa samostatnú hviezdičku StarIcon, nedávame znak ★ do textu:
                string textFormat = starIconObject != null
                    ? GetLocalized("vip_role_cap_no_star", "No {1} - max {0}", starCap, roleName)
                    : GetLocalized("vip_role_cap", "No {1} - max {0}★", starCap, roleName);

                roleCapText.text = textFormat;
                roleCapText.color = roleCapColor;
            }
        }

        // Stlmenie ľavej časti pri stave DONE
        ApplyDoneAlpha(state == VipRequirementState.Completed ? 0.55f : 1f);

        // Prepínanie štítku a akcie vpravo
        switch (state)
        {
            case VipRequirementState.Missing:
                SetChip(chipMissing, statusMissing, GetLocalized("vip_state_missing", "MISSING"), ColorMissing);
                ShowFixAction(playerMaterials, req.materials);
                HideProgressAction();
                break;

            case VipRequirementState.Ready:
                SetChip(chipReady, statusReady, GetLocalized("vip_state_ready", "READY"), ColorReady);
                HideFixAction();
                HideProgressAction();
                break;

            case VipRequirementState.InProgress:
                SetChip(chipInProgress, statusInProgress, GetLocalized("vip_state_in_progress", "IN PROGRESS"), ColorInProgress);
                HideFixAction();
                ShowProgressAction(remainingSec, totalDurationSec);
                break;

            case VipRequirementState.Completed:
                SetChip(chipCompleted, statusCompleted, GetLocalized("vip_state_completed", "DONE"), ColorCompleted);
                HideFixAction();
                HideProgressAction();
                break;
        }
    }

    public void UpdateProgressOnly(float remainingSec, float totalDurationSec)
    {
        ShowProgressAction(remainingSec, totalDurationSec);
    }

    private void SetChip(Sprite bgSprite, Sprite icoSprite, string label, Color txtColor)
    {
        if (stateBackground != null && bgSprite != null) stateBackground.sprite = bgSprite;
        if (stateIcon != null && icoSprite != null) stateIcon.sprite = icoSprite;
        if (stateText != null)
        {
            stateText.text = label;
            stateText.color = txtColor;
        }
    }

    private void ShowFixAction(int haveMaterials, int needMaterials)
    {
        if (fixButton != null)
        {
            fixButton.gameObject.SetActive(true);
            fixButton.interactable = true;
        }

        if (fixAmountText != null)
        {
            bool enough = haveMaterials >= needMaterials;
            fixAmountText.text = enough ? $"{needMaterials}" : $"{haveMaterials}/{needMaterials}";
            fixAmountText.color = enough ? ColorNormalText : ColorMissing;
        }
    }

    private void HideFixAction()
    {
        if (fixButton != null)
            fixButton.gameObject.SetActive(false);
    }

    private void ShowProgressAction(float remainingSec, float totalDurationSec)
    {
        float duration = Mathf.Max(0.1f, totalDurationSec);
        float progress = Mathf.Clamp01(1f - (remainingSec / duration));

        if (progressSlider != null)
        {
            progressSlider.gameObject.SetActive(true);
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = progress;

            if (progressSlider.fillRect != null)
                progressSlider.fillRect.gameObject.SetActive(progress > 0.01f);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            int totalSec = Mathf.CeilToInt(Mathf.Max(0f, remainingSec));
            timerText.text = $"{totalSec / 60}:{totalSec % 60:00}";
        }
    }

    private void HideProgressAction()
    {
        if (progressSlider != null)
            progressSlider.gameObject.SetActive(false);

        if (timerText != null)
            timerText.gameObject.SetActive(false);
    }

    private void ApplyDoneAlpha(float alpha)
    {
        if (leftContentCanvasGroup != null)
        {
            leftContentCanvasGroup.alpha = alpha;
            return;
        }

        SetGraphicAlpha(mainIcon, alpha);
        SetGraphicAlpha(requestNameText, alpha);
        if (locationNameText != null && locationNameText.transform.parent != null)
        {
            foreach (var g in locationNameText.transform.parent.GetComponentsInChildren<Graphic>(true))
                SetGraphicAlpha(g, alpha);
        }
    }

    private static void SetGraphicAlpha(Graphic g, float alpha)
    {
        if (g == null) return;
        Color c = g.color;
        c.a = alpha;
        g.color = c;
    }

    private static string GetDefaultLocationName(NeedType type) => type switch
    {
        NeedType.Rooms => "Reception",
        NeedType.Food => "Restaurant",
        NeedType.Drinks => "Beach Bar",
        NeedType.Relax => "Pool",
        NeedType.OceanView => "Bungalow",
        _ => "Resort"
    };

    private static string GetLocalized(string key, string fallback, params object[] args)
    {
        try
        {
            string s = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key, args);
            if (!string.IsNullOrWhiteSpace(s) &&
                !s.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }
        }
        catch { }

        return string.Format(fallback, args);
    }
}