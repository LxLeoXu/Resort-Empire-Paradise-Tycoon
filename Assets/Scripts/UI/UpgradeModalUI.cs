using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ModalWindow))]
public class UpgradeModalUI : MonoBehaviour
{
	[Header("BuildingCard")]
	[Tooltip("Window -> BuildingCard -> Icon")]
	[SerializeField] private Image buildingIcon;

	[Tooltip("Window -> BuildingCard -> Title")]
	[SerializeField] private TextMeshProUGUI buildingTitle;

	[Tooltip("Window -> BuildingCard -> Text")]
	[SerializeField] private TextMeshProUGUI buildingDescription;

	[Header("Capacity")]
	[Tooltip("Window -> Description -> Capacity -> LeftSide")]
	[SerializeField] private TextMeshProUGUI capacityLabel;

	[Tooltip("Window -> Description -> Capacity -> RightSide -> Before")]
	[SerializeField] private TextMeshProUGUI capacityBefore;

	[Tooltip("Window -> Description -> Capacity -> RightSide -> After")]
	[SerializeField] private TextMeshProUGUI capacityAfter;

	[Header("Quality")]
	[Tooltip("Window -> Description -> Quality -> LeftSide")]
	[SerializeField] private TextMeshProUGUI qualityLabel;

	[Tooltip("Window -> Description -> Quality -> RightSide -> Before")]
	[SerializeField] private TextMeshProUGUI qualityBefore;

	[Tooltip("Window -> Description -> Quality -> RightSide -> After")]
	[SerializeField] private TextMeshProUGUI qualityAfter;

	[Header("Resources")]
	[Tooltip("Window -> Description -> Resources -> LeftSide -> Text")]
	[SerializeField] private TextMeshProUGUI costText;

	[Tooltip("Window -> Description -> Resources -> RightSide -> Text")]
	[SerializeField] private TextMeshProUGUI resortValueText;

	[Header("Buttons")]
	[Tooltip("Window -> Buttons -> UpgradeButton")]
	[SerializeField] private Button upgradeButton;

	[Tooltip("Window -> Buttons -> LaterButton")]
	[SerializeField] private Button laterButton;

	private ModalWindow modalWindow;
	private BuildingInfo targetBuilding;

	private void Awake()
	{
		modalWindow = GetComponent<ModalWindow>();

		if (laterButton != null)
			laterButton.onClick.AddListener(OnLaterClicked);

		if (upgradeButton != null)
			upgradeButton.onClick.AddListener(OnUpgradeClicked);
	}

	/// <summary>
	/// Otvorí okno a naplní ho dátami z budovy
	/// </summary>
	public void OpenForBuilding(BuildingInfo building)
	{
		if (building == null || !building.CanUpgrade) return;

		targetBuilding = building;

		// 1. BuildingCard
		if (buildingTitle != null) buildingTitle.text = building.buildingName;
		if (buildingIcon != null && building.buildingIcon != null)
			buildingIcon.sprite = building.buildingIcon;
		if (buildingDescription != null)
			buildingDescription.text = $"Level up to unlock more {building.offeredNeed.ToString().ToLower()}!";

		// 2. Capacity (Before -> After)
		if (capacityLabel != null)
			capacityLabel.text = $"{building.offeredNeed} Capacity";
		if (capacityBefore != null)
			capacityBefore.text = $"{building.CurrentCapacity} → ";
		if (capacityAfter != null)
			capacityAfter.text = $"{building.NextCapacity}";

		// 3. Quality (Before -> After)
		if (qualityLabel != null)
			qualityLabel.text = $"{building.offeredNeed} Quality";
		if (qualityBefore != null)
			qualityBefore.text = building.currentLevel == 1 ? "Basic → " : "Improved → ";
		if (qualityAfter != null)
			qualityAfter.text = building.currentLevel == 1 ? "Improved" : "Premium";

		// 4. Resources
		int cost = building.UpgradeCost;
		if (costText != null)
			costText.text = $"{cost}";

		if (resortValueText != null)
			resortValueText.text = $"+{building.ResortValueGain} Resort Value";

		// 5. Kontrola mincí
		bool hasEnough = ResourceManager.Instance != null &&
						 ResourceManager.Instance.HasEnough(ResourceType.Coins, cost);

		if (upgradeButton != null)
			upgradeButton.interactable = hasEnough;

		// Zobrazenie okna
		modalWindow.Open();
	}

	private void OnUpgradeClicked()
	{
		if (targetBuilding == null || !targetBuilding.CanUpgrade) return;

		int cost = targetBuilding.UpgradeCost;
		int gain = targetBuilding.ResortValueGain;

		if (ResourceManager.Instance != null)
		{
			if (!ResourceManager.Instance.SpendResource(ResourceType.Coins, cost))
			{
				Debug.LogWarning("Nedostatok mincí na vylepšenie!");
				return;
			}

			ResourceManager.Instance.AddResource(ResourceType.ResortValue, gain);
		}

		// Zvýši úroveň budovy
		targetBuilding.ApplyUpgrade();

		// Zavrie okno
		modalWindow.Close();
	}

	private void OnLaterClicked()
	{
		modalWindow.Close();
	}
}