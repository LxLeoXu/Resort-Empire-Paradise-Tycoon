using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class S4FigmaResultsUI
{
    private bool vipVariant;
    private bool vipVisualsPrepared;

    private TMP_Text reputationRewardText;
    private TMP_Text gemsRewardText;

    private GameObject perfectStayBanner;

    private static readonly Color VipPurple =
        new Color32(126, 51, 242, 255);

    private static readonly Color VipDarkPurple =
        new Color32(103, 35, 210, 255);

    public bool IsVipVariant => vipVariant;

    public static readonly ResourceType[] VipRewardOrder =
    {
        ResourceType.Reputation,
        ResourceType.Coins,
        ResourceType.Energy,
        ResourceType.Materials,
        ResourceType.ResortValue
    };

    // =====================================================
    // CREATE S8 FROM EXISTING S4
    // =====================================================

    public S4FigmaResultsUI CreateVipVariant(
        VipGuestController owner)
    {
        GameObject clone = Instantiate(
            gameObject,
            transform.parent,
            false
        );

        clone.name = "S8_VipResults";

        var view = clone.GetComponent<S4FigmaResultsUI>();

        view.vipVariant = true;

        foreach (TMP_Text text in
                 clone.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == "GemsRewardText")
            {
                view.gemsRewardText = text;
                text.gameObject.SetActive(false);
            }
        }

        // Use existing VIP Claim logic.
        if (view.claimRewards != null)
        {
            view.claimRewards.BindVipResult(owner);
        }
        else
        {
            Debug.LogError(
                "S8: ClaimRewards component is missing.",
                clone
            );
        }

        view.hideOnStart = false;

        clone.SetActive(false);

        return view;
    }

    // =====================================================
    // EXTRA REWARD ROWS
    // =====================================================

    private TMP_Text ExtraReward(
        ref TMP_Text field,
        string name)
    {
        if (field == null && materialsRewardText != null)
        {
            field = Instantiate(
                materialsRewardText,
                materialsRewardText.transform.parent,
                false
            );

            field.gameObject.name = name;
        }

        return field;
    }

    private static void LayoutRewards(
        params TMP_Text[] rows)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null)
                continue;

            rows[i].rectTransform.anchoredPosition =
                new Vector2(0, 64 - 44 * i);

            rows[i].rectTransform.sizeDelta =
                new Vector2(490, 36);

            rows[i].enableAutoSizing = true;
            rows[i].fontSizeMin = 18;
            rows[i].fontSizeMax = 27;
        }
    }

    // =====================================================
    // NORMAL S4 GEMS
    // =====================================================

    private void ShowGemReward(int amount)
    {
        if (amount > 0)
        {
            TMP_Text text = ExtraReward(
                ref gemsRewardText,
                "GemsRewardText"
            );

            if (text != null)
            {
                text.gameObject.SetActive(true);
                text.text = "+" + amount + " Gems";
            }

            LayoutRewards(
                coinsRewardText,
                energyRewardText,
                resortValueRewardText,
                materialsRewardText,
                gemsRewardText
            );
        }
        else if (gemsRewardText != null)
        {
            gemsRewardText.gameObject.SetActive(false);
        }
        LayoutOrdinaryS4Rewards();
    }
    // =====================================================
// S4 - NORMAL WAVE REWARDS LAYOUT
// SCRUM-531 / SCRUM-534
// Prevent overlap between Coins Breakdown and Energy.
// Does not modify VIP S8 layout.
// =====================================================

private void LayoutOrdinaryS4Rewards()
{
    if (IsVipVariant || coinsRewardText == null)
        return;

    // Use the existing Coins position as reference.
    float topY =
        coinsRewardText.rectTransform.anchoredPosition.y;

    // Coins remain in their original position.

    // Coins breakdown appears immediately below Coins.
    SetOrdinaryRewardPosition(
        coinsBreakdownText,
        topY - 28f,
        24f
    );

    // Move the following rewards below the breakdown.
    SetOrdinaryRewardPosition(
        energyRewardText,
        topY - 64f,
        34f
    );

    SetOrdinaryRewardPosition(
        resortValueRewardText,
        topY - 104f,
        34f
    );

    SetOrdinaryRewardPosition(
        materialsRewardText,
        topY - 144f,
        34f
    );

    // Gems appear only when awarded.
    if (gemsRewardText != null &&
        gemsRewardText.gameObject.activeSelf)
    {
        SetOrdinaryRewardPosition(
            gemsRewardText,
            topY - 184f,
            34f
        );
    }

    // Keep breakdown text compact and readable.
    if (coinsBreakdownText != null)
    {
        coinsBreakdownText.enableAutoSizing = true;
        coinsBreakdownText.fontSizeMin = 13f;
        coinsBreakdownText.fontSizeMax = 16f;

        coinsBreakdownText.alignment =
            TextAlignmentOptions.Left;
    }
}

private static void SetOrdinaryRewardPosition(
    TMP_Text text,
    float y,
    float height)
{
    if (text == null)
        return;

    RectTransform rect = text.rectTransform;

    Vector2 position = rect.anchoredPosition;
    position.y = y;

    rect.anchoredPosition = position;

    Vector2 size = rect.sizeDelta;
    size.y = height;

    rect.sizeDelta = size;
}

    // =====================================================
    // FIND TEXT
    // =====================================================

    private TMP_Text FindNamedText(string name)
    {
        foreach (TMP_Text text in
                 GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name == name)
                return text;
        }

        return null;
    }

    private void SetNamedText(
        string name,
        string value)
    {
        TMP_Text text = FindNamedText(name);

        if (text != null)
            text.text = value;
    }

    // =====================================================
    // VIP VISUAL DESIGN
    // =====================================================

    private void PrepareVipVisuals()
    {
        if (!vipVariant || vipVisualsPrepared)
            return;

        vipVisualsPrepared = true;

        // Purple panel borders.
        foreach (Image image in
                 GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject.name ==
                    "PerformancePanel_Border" ||
                image.gameObject.name ==
                    "RewardsPanel_Border")
            {
                image.color = VipPurple;
            }
        }

        // Purple Claim Rewards button.
        foreach (Button button in
                 GetComponentsInChildren<Button>(true))
        {
            if (button.name.IndexOf(
                    "Claim",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Image background =
                button.targetGraphic as Image;

            if (background != null)
                background.color = VipPurple;

            ColorBlock colors = button.colors;

            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(0.94f, 0.94f, 0.94f);
            colors.pressedColor =
                new Color(0.65f, 0.65f, 0.65f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.05f;

            button.transition =
                Selectable.Transition.ColorTint;

            button.colors = colors;
        }

        CreatePerfectStayBanner();
    }

    // =====================================================
    // PERFECT STAY BANNER
    // =====================================================

    private void CreatePerfectStayBanner()
    {
        TMP_Text title = FindNamedText("TitleText");

        if (title == null)
        {
            Debug.LogWarning(
                "S8: TitleText not found. Banner was skipped.",
                this
            );

            return;
        }

        GameObject banner = new GameObject(
            "VIPPerfectStayBanner",
            typeof(RectTransform),
            typeof(Image)
        );

        banner.transform.SetParent(
            title.transform.parent,
            false
        );

        RectTransform rect =
            banner.GetComponent<RectTransform>();

        RectTransform titleRect =
            title.rectTransform;

        rect.anchorMin = titleRect.anchorMin;
        rect.anchorMax = titleRect.anchorMax;
        rect.pivot = titleRect.pivot;

        rect.anchoredPosition =
            titleRect.anchoredPosition +
            new Vector2(0, 55);

        rect.sizeDelta = new Vector2(530, 42);

        Image background = banner.GetComponent<Image>();

        background.color = VipDarkPurple;
        background.raycastTarget = false;

        TMP_Text bannerText = Instantiate(
            title,
            banner.transform,
            false
        );

        bannerText.gameObject.name =
            "VIPPerfectStayBannerText";

        bannerText.text =
            "PERFECT STAY BONUS UNLOCKED";

        bannerText.color = Color.white;
        bannerText.fontSize = 25;
        bannerText.fontStyle = FontStyles.Bold;

        bannerText.alignment =
            TextAlignmentOptions.Center;

        RectTransform textRect =
            bannerText.rectTransform;

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        banner.transform.SetAsLastSibling();

        perfectStayBanner = banner;
    }

    // =====================================================
    // DISPLAY VIP RESULT
    // =====================================================

    public void ShowVipResults(VipStayResult result)
    {
        if (!vipVariant ||
            result == null ||
            !result.IsValid())
        {
            return;
        }

        PrepareVipVisuals();

        // ---------------------------------
        // HEADER
        // ---------------------------------

        SetNamedText(
            "TitleText",
            "VIP VISIT COMPLETE!"
        );

        SetNamedText(
            "PerformanceTitle",
            result.guestName + "'S FEEDBACK"
        );

        SetNamedText(
            "GuestsLabel",
            "Required fulfilled"
        );

        SetNamedText(
            "SatisfactionLabel",
            "VIP rating"
        );

        // Rename reward heading if it exists.
        TMP_Text rewardsTitle =
            FindNamedText("RewardsTitle");

        if (rewardsTitle != null)
        {
            rewardsTitle.text =
                "PREMIUM REWARDS";

            rewardsTitle.color = VipPurple;
        }

        // ---------------------------------
        // PERFORMANCE
        // ---------------------------------

        if (guestsText != null)
        {
            guestsText.text =
                result.requiredCompleted + "/3";
        }

        if (satisfactionText != null)
        {
            satisfactionText.text =
                result.stars + "/5";
        }

        if (statusLine1Text != null)
        {
            statusLine1Text.gameObject.SetActive(true);

            statusLine1Text.text =
                string.Join("\n", result.requirements);

            statusLine1Text.rectTransform.anchoredPosition =
                new Vector2(0, -60);

            statusLine1Text.rectTransform.sizeDelta =
                new Vector2(505, 100);

            statusLine1Text.enableAutoSizing = true;
            statusLine1Text.fontSizeMin = 15;
            statusLine1Text.fontSizeMax = 21;
        }

        if (statusLine2Text != null)
        {
            statusLine2Text.gameObject.SetActive(true);

            statusLine2Text.text =
                result.perfectStay
                    ? "PERFECT STAY!"
                    : "STAY COMPLETE";

            statusLine2Text.color =
                result.perfectStay
                    ? VipPurple
                    : Color.gray;

            statusLine2Text.rectTransform.anchoredPosition =
                new Vector2(0, -128);

            statusLine2Text.rectTransform.sizeDelta =
                new Vector2(505, 26);

            statusLine2Text.fontSize = 21;
        }

        // ---------------------------------
        // PERFECT STAY BANNER
        // ---------------------------------

        if (perfectStayBanner != null)
        {
            perfectStayBanner.SetActive(
                result.perfectStay
            );
        }

        // ---------------------------------
        // VIP REWARDS
        // ---------------------------------

        TMP_Text reputation = ExtraReward(
            ref reputationRewardText,
            "ReputationRewardText"
        );

        if (reputation != null)
        {
            reputation.text =
                "+" + result.rewards.reputation +
                " Reputation";

            reputation.color =
                new Color32(190, 65, 0, 255);
        }

        if (coinsRewardText != null)
        {
            coinsRewardText.text =
                "+" + result.rewards.coins +
                " Coins";

            coinsRewardText.color =
                new Color32(0, 175, 30, 255);
        }

        if (energyRewardText != null)
        {
            energyRewardText.text =
                "+" + result.rewards.energy +
                " Energy";

            energyRewardText.color =
                new Color32(0, 175, 210, 255);
        }

        if (materialsRewardText != null)
        {
            materialsRewardText.text =
                "+" + result.rewards.materials +
                " Materials";

            materialsRewardText.color =
                VipPurple;
        }

        if (resortValueRewardText != null)
        {
            resortValueRewardText.text =
                "+" + result.rewards.resortValue +
                " Resort Value";

            resortValueRewardText.color =
                new Color32(240, 170, 0, 255);
        }

        if (gemsRewardText != null)
        {
            gemsRewardText.gameObject.SetActive(false);
        }

        // VIP does not display regular-wave
        // coin calculation breakdown.
        foreach (TMP_Text text in
                 GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name.IndexOf(
                    "CoinsBreakdown",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text.gameObject.SetActive(false);
            }
        }

        LayoutRewards(
            reputation,
            coinsRewardText,
            energyRewardText,
            materialsRewardText,
            resortValueRewardText
        );

        // ---------------------------------
        // STARS
        // ---------------------------------

        if (starImages != null)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null)
                    continue;

                starImages[i].color =
                    i < result.stars
                        ? activeStarColor
                        : inactiveStarColor;
            }
        }

        resultsShown = true;

        gameObject.SetActive(true);
    }
}