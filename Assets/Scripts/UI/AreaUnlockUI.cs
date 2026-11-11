using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;

public enum AreaUnlockState
{
    Locked,
    Available,
    NotAffordable,
    Unlocked
}

public class AreaUnlockUI : MonoBehaviour
{
    private const string TableName = "GameTexts";

    [Header("Dynamický obsah")]
    [SerializeField] private TMP_Text headerBadgeText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text demandText;
    [SerializeField] private TMP_Text satisfactionText;
    [SerializeField] private TMP_Text workerText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image previewImage;

    [Header("Tlačidlo nákupu")]
    [SerializeField] private Button unlockButton;
    [SerializeField] private TMP_Text unlockButtonText;

    // Uložená akcia VÝHRADNE pre aktuálne otvorené stavenisko
    private Action currentUnlockAction = null;

    private void Awake()
    {
        if (unlockButton != null)
        {
            unlockButton.onClick.RemoveAllListeners();
            unlockButton.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnDisable()
    {
        // Po zatvorení okna okamžite zmažeme prepojenie na stavenisko
        currentUnlockAction = null;
    }

    private void OnButtonClicked()
    {
        if (currentUnlockAction != null)
        {
            Action actionToExecute = currentUnlockAction;
            currentUnlockAction = null; // Ochrana pred dvojitým klikom
            actionToExecute.Invoke();
        }
    }

    /// <summary>
    /// Nastaví dáta a priradí nákup LEN pre jedno konkrétne stavenisko
    /// </summary>
    public void Setup(string title, Sprite preview, string demand, int cost, bool isBuilt, bool isEligible, int playerCoins, Action onConfirmAction)
    {
        // Nastavíme callback len pre túto konkrétnu budovu
        currentUnlockAction = onConfirmAction;

        if (titleText != null) titleText.text = title;
        if (previewImage != null && preview != null) previewImage.sprite = preview;
        if (priceText != null) priceText.text = $"{cost:N0} Coins";

        if (demandText != null)
            demandText.text = $"✔ Adds: {demand}";

        if (satisfactionText != null)
            satisfactionText.text = $"✔ {GetLocalized("s9_feature_satisfaction", "Higher satisfaction potential")}";

        if (workerText != null)
            workerText.text = $"✔ {GetLocalized("s9_feature_worker", "Includes: 1 Worker")}";

        if (isBuilt)
        {
            unlockButton.interactable = false;
            unlockButtonText.text = GetLocalized("s9_btn_already_built", "ALREADY BUILT");
            if (headerBadgeText != null) 
                headerBadgeText.text = GetLocalized("s9_badge_available", "NEW AREA AVAILABLE !");
        }
        else if (!isEligible)
        {
            unlockButton.interactable = false;
            unlockButtonText.text = GetLocalized("s9_btn_locked", "LOCKED");
            if (headerBadgeText != null) 
                headerBadgeText.text = GetLocalized("s9_badge_locked", "AREA LOCKED");
        }
        else if (playerCoins < cost)
        {
            unlockButton.interactable = false;
            int missing = cost - playerCoins;
            unlockButtonText.text = GetLocalized("s9_btn_missing_coins", $"MISSING: {missing:N0} COINS", missing);
            if (headerBadgeText != null) 
                headerBadgeText.text = GetLocalized("s9_badge_available", "NEW AREA AVAILABLE !");
        }
        else
        {
            unlockButton.interactable = true;
            unlockButtonText.text = GetLocalized("s9_btn_unlock", "UNLOCK & EXPAND");
            if (headerBadgeText != null) 
                headerBadgeText.text = GetLocalized("s9_badge_available", "NEW AREA AVAILABLE !");
        }
    }

    private string GetLocalized(string key, string fallback, params object[] args)
    {
        try
        {
            string localized = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key, args);
            return string.IsNullOrEmpty(localized) ? string.Format(fallback, args) : localized;
        }
        catch
        {
            return string.Format(fallback, args);
        }
    }
}