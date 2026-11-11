using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ClaimRewards : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button claimButton;

    [Header("Prechod na S5")]
    [Tooltip("Sem pretiahni prefab/objekt S5_UpgradeModal")]
    [SerializeField] private UpgradeModalUI upgradeModal;
    [Tooltip("Sem pretiahni celé okno S4 (aby sa po Claim zatvorilo)")]
    [SerializeField] private GameObject s4Root;

    private WaveRewardsData pendingRewards;
    private bool rewardPrepared = false;
    private bool claimed = false;
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

    public void SetRewards(WaveRewardsData rewards)
    {
        if (rewardPrepared || claimed) return;
        Prepare(rewards);
    }

    public bool SetRewardsForWave(int waveId, WaveRewardsData rewards)
    {
        if (waveId < 0 || waveId <= lastWaveId || (rewardPrepared && !claimed) || !IsValid(rewards))
            return false;

        lastWaveId = waveId;
        Prepare(rewards);
        return true;
    }

    public void SetReward(int servedGuests, int satisfactionPercent)
    {
        SetRewards(WaveRewardCalculator.CalculateRewards(servedGuests, satisfactionPercent));
    }

    public bool SetRewardForWave(int waveId, int servedGuests, int satisfactionPercent)
    {
        return SetRewardsForWave(waveId, WaveRewardCalculator.CalculateRewards(servedGuests, satisfactionPercent));
    }

    private static bool IsValid(WaveRewardsData rewards)
    {
        return rewards != null && rewards.coins >= 0 && rewards.energy >= 0 && rewards.resortValue >= 0;
    }

    private void Prepare(WaveRewardsData rewards)
    {
        if (!IsValid(rewards)) return;

        pendingRewards = new WaveRewardsData(rewards.coins, rewards.energy, rewards.resortValue);
        rewardPrepared = true;
        claimed = false;
        RefreshButton();
    }

    public void Claim()
    {
        if (!rewardPrepared || claimed || pendingRewards == null) return;

        ResourceManager manager = ResourceManager.Instance;
        if (manager == null) return;

        int coins = pendingRewards.coins;
        int energy = pendingRewards.energy;
        int resort = pendingRewards.resortValue;

        int currentEnergy = manager.GetResource(ResourceType.Energy);
        int freeEnergy = Mathf.Max(0, ResourceManager.MaxEnergy - currentEnergy);
        int energyToAdd = Mathf.Min(energy, freeEnergy);

        claimed = true;
        rewardPrepared = false;
        RefreshButton();

        manager.AddResource(ResourceType.Coins, coins);
        manager.AddResource(ResourceType.ResortValue, resort);
        if (energyToAdd > 0) manager.AddResource(ResourceType.Energy, energyToAdd);

        Debug.Log($"<color=green>[CLAIM]</color> Pripísané: +{coins} Coins, +{energyToAdd} Energy, +{resort} Resort Value");

        OnClaimed?.Invoke();

        // 1. ZAVRIEME OKNO S4
        if (s4Root != null)
        {
            ModalWindow modal = s4Root.GetComponent<ModalWindow>();
            if (modal != null) modal.Close();
            else s4Root.SetActive(false);
        }

        // 2. OTVORÍME OKNO S5
        if (upgradeModal != null)
        {
            upgradeModal.OpenRecommended();
        }
        else
        {
            Debug.LogWarning("ClaimRewards nemá priradený UpgradeModalUI (S5)!");
        }
    }

    private void RefreshButton()
    {
        if (claimButton != null)
            claimButton.interactable = rewardPrepared && !claimed;
    }
}