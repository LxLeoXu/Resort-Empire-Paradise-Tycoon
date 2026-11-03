using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ClaimRewards : MonoBehaviour
{
    [SerializeField] private Button claimButton;

    private WaveRewardsData pendingRewards;
    private bool rewardPrepared;
    private bool claimed;
    private int lastWaveId = -1;

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

    // Jednorazove pripravenie pri testovani S4 (bez identifikatora vlny).
    public void SetRewards(WaveRewardsData rewards)
    {
        if (rewardPrepared || claimed)
        {
            Debug.LogWarning("ClaimRewards: Odmena uz bola pripravena alebo vyzdvihnuta.");
            return;
        }
        Prepare(rewards);
    }

    // Pre realnu hru: novu odmenu pripravi iba pre NOVE ID vlny.
    public bool SetRewardsForWave(int waveId, WaveRewardsData rewards)
    {
        if (waveId < 0 || waveId <= lastWaveId || (rewardPrepared && !claimed) || rewards == null)
            return false;

        lastWaveId = waveId;
        Prepare(rewards);
        return true;
    }

    // Zachovanie volani zo starsich testerov a UI.
    public void SetReward(int servedGuests, int satisfactionPercent)
    {
        SetRewards(WaveRewardCalculator.CalculateRewards(servedGuests, satisfactionPercent));
    }

    public bool SetRewardForWave(int waveId, int servedGuests, int satisfactionPercent)
    {
        return SetRewardsForWave(
            waveId,
            WaveRewardCalculator.CalculateRewards(servedGuests, satisfactionPercent));
    }

    private void Prepare(WaveRewardsData rewards)
    {
        if (rewards == null)
        {
            Debug.LogError("ClaimRewards: RewardsData je null.");
            return;
        }

        // Ulozime kopiu, aby externe zmeny neovplyvnili pripravenu odmenu.
        pendingRewards = new WaveRewardsData(rewards.coins, rewards.energy, rewards.resortValue);
        rewardPrepared = true;
        claimed = false;
        RefreshButton();
        Debug.Log($"Pripravene odmeny: +{pendingRewards.coins} Coins, +{pendingRewards.energy} Energy, +{pendingRewards.resortValue} Resort Value");
    }

    public void Claim()
    {
        if (!rewardPrepared || claimed || pendingRewards == null)
            return;

        ResourceManager manager = ResourceManager.Instance;
        if (manager == null)
        {
            Debug.LogError("ClaimRewards: ResourceManager neexistuje.");
            return;
        }

        int coins = pendingRewards.coins;
        int energy = pendingRewards.energy;
        int resort = pendingRewards.resortValue;

        // Overenie pred pripisovanim. Energy ma v ResourceManager limite 50.
        if (!Fits(manager, ResourceType.Coins, coins) ||
            !Fits(manager, ResourceType.ResortValue, resort))
        {
            Debug.LogError("ClaimRewards: Odmena by prekrocila maximum typu int.");
            return;
        }

        // Zablokujeme dalsie volania este pred vyvolanim OnResourceChanged.
        claimed = true;
        rewardPrepared = false;
        RefreshButton();

        bool coinsOk = coins == 0 || manager.AddResource(ResourceType.Coins, coins);
        bool resortOk = resort == 0 || manager.AddResource(ResourceType.ResortValue, resort);

        int energyBefore = manager.GetResource(ResourceType.Energy);
        int freeEnergySpace = Mathf.Max(0, ResourceManager.MaxEnergy - energyBefore);
        int energyAdded = Mathf.Min(energy, freeEnergySpace);
        bool energyOk = energyAdded == 0 || manager.AddResource(ResourceType.Energy, energyAdded);

        if (!coinsOk || !resortOk || !energyOk)
        {
            // Nic nepripisujeme druhy raz: ak by prislo k necakanej chybe,
            // treba ju opravit, nie opakovat kliknutie po ciastocnej vyplate.
            Debug.LogError("ClaimRewards: Chyba pri pripisovani jednej z odmien. Skontroluj zdroje.");
            return;
        }

        Debug.Log($"Claimed: +{coins} Coins, +{energyAdded} Energy, +{resort} Resort Value");
        OnClaimed?.Invoke();
    }

    private static bool Fits(ResourceManager manager, ResourceType type, int amount)
    {
        return amount >= 0 && amount <= int.MaxValue - manager.GetResource(type);
    }

    private void RefreshButton()
    {
        if (claimButton != null)
            claimButton.interactable = rewardPrepared && !claimed;
    }
}
