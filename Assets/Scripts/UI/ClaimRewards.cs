using UnityEngine;
using UnityEngine.UI;

public class ClaimRewards : MonoBehaviour
{
    [SerializeField] private Button claimButton;

    private WaveRewardsData pendingRewards;
    private bool claimed = false;

    private void Awake()
    {
        if (claimButton != null)
            claimButton.onClick.AddListener(Claim);
    }

    public void SetRewards(WaveRewardsData rewards)
    {
        pendingRewards = rewards;

        claimed = false;

        if (claimButton != null)
            claimButton.interactable = true;

        Debug.Log(
            $"Rewards prepared: Coins={rewards.coins}, Energy={rewards.energy}, ResortValue={rewards.resortValue}");
    }

    public void Claim()
    {
        if (claimed)
            return;

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager not found!");
            return;
        }

        if (pendingRewards == null)
        {
            Debug.LogWarning("Rewards are not prepared.");
            return;
        }

        ResourceManager.Instance.AddResource(ResourceType.Coins, pendingRewards.coins);
        ResourceManager.Instance.AddResource(ResourceType.Energy, pendingRewards.energy);
        ResourceManager.Instance.AddResource(ResourceType.ResortValue, pendingRewards.resortValue);

        claimed = true;

        if (claimButton != null)
            claimButton.interactable = false;

        Debug.Log("Rewards claimed successfully.");
    }

    private void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(Claim);
    }
}