using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AreaUnlockUI : MonoBehaviour
{
    [Header("Dynamický obsah")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text demandText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image previewImage;

    [Header("Tlačidlo nákupu")]
    [SerializeField] private Button unlockButton;
    [SerializeField] private TMP_Text unlockButtonText;

    public event Action OnUnlockClicked;

    private void Awake()
    {
        if (unlockButton != null)
            unlockButton.onClick.AddListener(() => OnUnlockClicked?.Invoke());
    }

    /// <summary>
    /// Nastaví dynamické dáta a stav tlačidla podľa peňazí a postupu
    /// </summary>
    public void Setup(string title, Sprite preview, string demand, int cost, bool isBuilt, bool isEligible, int playerCoins)
    {
        if (titleText != null) titleText.text = title;
        if (previewImage != null && preview != null) previewImage.sprite = preview;
        if (demandText != null) demandText.text = $"Adds: {demand}";
        if (priceText != null) priceText.text = $"{cost:N0} Coins";

        // Nastavenie stavu tlačidla
        if (isBuilt)
        {
            unlockButton.interactable = false;
            unlockButtonText.text = "ALREADY BUILT";
        }
        else if (!isEligible)
        {
            unlockButton.interactable = false;
            unlockButtonText.text = "LOCKED";
        }
        else if (playerCoins < cost)
        {
            unlockButton.interactable = false;
            int missing = cost - playerCoins;
            unlockButtonText.text = $"MISSING: {missing:N0} COINS";
        }
        else
        {
            unlockButton.interactable = true;
            unlockButtonText.text = "UNLOCK & EXPAND";
        }
    }
}