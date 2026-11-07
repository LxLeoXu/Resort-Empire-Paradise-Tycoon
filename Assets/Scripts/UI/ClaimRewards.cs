
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ClaimRewards : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button claimButton;

    private WaveRewardsData pendingRewards;

    private bool rewardPrepared = false;
    private bool claimed = false;

    private int lastWaveId = -1;

    // Udalost po uspesnom Claim Rewards
    public event Action OnClaimed;

    private void OnEnable()
    {
        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(Claim);
            claimButton.onClick.AddListener(Claim);
        }

        RefreshButton();
    }

    private void OnDisable()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(Claim);
    }

    // Testovanie bez cisla vlny
    public void SetRewards(WaveRewardsData rewards)
    {
        if (rewardPrepared || claimed)
        {
            Debug.LogWarning(
                "ClaimRewards: Odmena uz bola pripravena.");
            return;
        }

        Prepare(rewards);
    }

    // Odmeny pre skutocnu vlnu
    public bool SetRewardsForWave(
        int waveId,
        WaveRewardsData rewards)
    {
        if (waveId < 0 ||
            waveId <= lastWaveId ||
            (rewardPrepared && !claimed) ||
            !IsValid(rewards))
        {
            return false;
        }

        lastWaveId = waveId;
        Prepare(rewards);

        return true;
    }

    // Kompatibilita so starsimi testermi
    public void SetReward(
        int servedGuests,
        int satisfactionPercent)
    {
        SetRewards(
            WaveRewardCalculator.CalculateRewards(
                servedGuests,
                satisfactionPercent));
    }

    public bool SetRewardForWave(
        int waveId,
        int servedGuests,
        int satisfactionPercent)
    {
        return SetRewardsForWave(
            waveId,
            WaveRewardCalculator.CalculateRewards(
                servedGuests,
                satisfactionPercent));
    }

    private static bool IsValid(WaveRewardsData rewards)
    {
        return rewards != null &&
               rewards.coins >= 0 &&
               rewards.energy >= 0 &&
               rewards.resortValue >= 0;
    }

    private void Prepare(WaveRewardsData rewards)
    {
        if (!IsValid(rewards))
        {
            Debug.LogError(
                "ClaimRewards: Neplatne odmeny.");
            return;
        }

        // Kopia odmien
        pendingRewards = new WaveRewardsData(
            rewards.coins,
            rewards.energy,
            rewards.resortValue);

        rewardPrepared = true;
        claimed = false;

        RefreshButton();

        Debug.Log(
            $"Rewards prepared: " +
            $"+{pendingRewards.coins} Coins, " +
            $"+{pendingRewards.energy} Energy, " +
            $"+{pendingRewards.resortValue} Resort Value");
    }

    public void Claim()
    {
        if (!rewardPrepared ||
            claimed ||
            pendingRewards == null)
        {
            return;
        }

        ResourceManager manager =
            ResourceManager.Instance;

        if (manager == null)
        {
            Debug.LogError(
                "ClaimRewards: ResourceManager not found!");
            return;
        }

        int coins = pendingRewards.coins;
        int energy = pendingRewards.energy;
        int resort = pendingRewards.resortValue;

        // Ochrana pred pretecenim hodnot
        if (!Fits(manager, ResourceType.Coins, coins) ||
            !Fits(manager, ResourceType.ResortValue, resort))
        {
            Debug.LogError(
                "ClaimRewards: Odmena prekrocila limit.");
            return;
        }

        int currentEnergy =
            manager.GetResource(ResourceType.Energy);

        int freeEnergy = Mathf.Max(
            0,
            ResourceManager.MaxEnergy - currentEnergy);

        int energyToAdd = Mathf.Min(
            energy,
            freeEnergy);

        // Zablokovanie opakovaneho Claim
        claimed = true;
        rewardPrepared = false;

        RefreshButton();

        bool coinsOK = coins == 0 ||
            manager.AddResource(ResourceType.Coins, coins);

        bool resortOK = resort == 0 ||
            manager.AddResource(ResourceType.ResortValue, resort);

        bool energyOK = energyToAdd == 0 ||
            manager.AddResource(
                ResourceType.Energy, energyToAdd);

        if (!coinsOK || !resortOK || !energyOK)
        {
            Debug.LogError(
                "ClaimRewards: Chyba pri pripisovani odmien.");
            return;
        }

        Debug.Log(
            $"Claimed: +{coins} Coins, " +
            $"+{energyToAdd} Energy, " +
            $"+{resort} Resort Value");

        // Oznamenie pre S4 a testovaci skript
        OnClaimed?.Invoke();
    }

    private static bool Fits(
        ResourceManager manager,
        ResourceType type,
        int amount)
    {
        return amount >= 0 &&
               amount <= int.MaxValue -
               manager.GetResource(type);
    }

    private void RefreshButton()
    {
        if (claimButton != null)
        {
            claimButton.interactable =
                rewardPrepared && !claimed;
        }
    }
}
