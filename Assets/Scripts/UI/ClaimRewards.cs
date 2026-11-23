using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public partial class ClaimRewards : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button claimButton;

    [Header("Prechod na S5")]
    [Tooltip("Sem pretiahni prefab/objekt S5_UpgradeModal")]
    [SerializeField] private UpgradeModalUI upgradeModal;
    [Tooltip("Sem pretiahni celé okno S4 (aby sa po Claim zatvorilo)")]
    [SerializeField] private GameObject s4Root;

    private VipGuestController vipOwner;
    public bool IsVipResult => vipOwner != null;
    public void BindVipResult(VipGuestController owner) { vipOwner = owner; RefreshButton(); }
    public static ClaimRewards FindOrdinaryClaim()
    {
        foreach (var claim in FindObjectsByType<ClaimRewards>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!claim.IsVipResult) return claim;
        return null;
    }
    public bool HasPendingFirstFiveStarBonus => HasPendingReward && pendingRewards.firstFiveStarBonus;
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

    private bool vipDisplayOnlyMode = false;

    public void PrepareVipDisplayOnly()
    {
        // The VIP credit was granted in S7; keep any ordinary pending wave intact.
        vipDisplayOnlyMode = true;
        RefreshButton();
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

    public bool HasClaimedWave(int waveId) => claimed && lastWaveId == waveId;

    public void BeginTestWave(int waveId)
    {
        if (claimed && waveId <= lastWaveId)
        {
            lastWaveId = waveId - 1;
            claimed = false;
            pendingRewards = null;
        }
    }

    public bool PrepareCompletedWave(int waveId, WaveRewardsData rewards)
    {
        if (!IsValid(rewards)) return false;
        if (!HasPendingReward || waveId <= lastWaveId) return SetRewardsForWave(waveId, rewards);
        // Older versions allowed starting another wave with an unpaid result.
        // Include that balance in the displayed reward and settle it with one Claim.
        rewards.coins += pendingRewards.coins;
        rewards.energy = Mathf.Min(rewards.energy + pendingRewards.energy,
            Mathf.Max(0, ResourceManager.MaxEnergy - ResourceManager.Instance.GetResource(ResourceType.Energy)));
        rewards.resortValue += pendingRewards.resortValue;
        rewards.materials += pendingRewards.materials;
        rewards.reputation += pendingRewards.reputation;
        rewards.gems = rewards.firstFiveStarBonus && pendingRewards.firstFiveStarBonus
            ? pendingRewards.gems : rewards.gems + pendingRewards.gems;
        rewards.firstFiveStarBonus |= pendingRewards.firstFiveStarBonus;
        lastWaveId = waveId;
        Prepare(rewards);
        return true;
    }

    private static bool IsValid(WaveRewardsData rewards) => rewards != null && rewards.IsValid();

    private void Prepare(WaveRewardsData rewards)
{
    if (!IsValid(rewards))
        return;

    pendingRewards = rewards.Copy();
    vipDisplayOnlyMode = false;

    rewardPrepared = true;
    claimed = false;

    RefreshButton();
}

    // Both S4 and S8 use this resource-credit path. Validate every balance before changing any.
    public static bool TryGrantResources(ResourceManager manager, WaveRewardsData rewards)
    {
        if (manager == null || !IsValid(rewards)) return false;
        ResourceType[] types = { ResourceType.Reputation, ResourceType.Coins, ResourceType.Energy,
            ResourceType.Materials, ResourceType.ResortValue, ResourceType.Gems };
        int[] amounts = { rewards.reputation, rewards.coins, rewards.energy, rewards.materials, rewards.resortValue, rewards.gems };
        for (int i = 0; i < types.Length; i++)
            if (types[i] != ResourceType.Energy && amounts[i] > int.MaxValue - manager.GetResource(types[i])) return false;
        var before = manager.CaptureResources();
        for (int i = 0; i < types.Length; i++)
        {
            int amount = types[i] == ResourceType.Energy
                ? Mathf.Min(amounts[i], Mathf.Max(0, ResourceManager.MaxEnergy - manager.GetResource(ResourceType.Energy))) : amounts[i];
            if (amount > 0 && !manager.AddResource(types[i], amount)) { manager.RestoreResources(before); return false; }
        }
        return true;
    }

    public void Claim()
    {
        if (vipDisplayOnlyMode)
        {
            // S7 already paid and persisted; this only closes the fallback UI.
            vipDisplayOnlyMode = false;
            RefreshButton();
            OnClaimed?.Invoke();
            return;
        }

        if (!rewardPrepared || claimed || pendingRewards == null) return;
        var manager = ResourceManager.Instance;
        if (manager == null || GamePersistence.IsRestoring) return;
        var before = manager.CaptureResources();
        var milestone = manager.CaptureFirstFiveStarReward();
        var credit = pendingRewards.Copy();
        if (credit.firstFiveStarBonus && manager.FirstFiveStarRewardClaimed) credit.gems = 0;
        credit.firstFiveStarBonus = false;
        credit.energy = Mathf.Min(credit.energy, Mathf.Max(0, ResourceManager.MaxEnergy - manager.GetResource(ResourceType.Energy)));
        claimed = true; rewardPrepared = false;
        RefreshButton();
        bool granted = TryGrantResources(manager, credit);
        if (granted && pendingRewards.firstFiveStarBonus && credit.gems > 0) manager.MarkFirstFiveStarRewardClaimed();
        bool saved = granted && (GamePersistence.Instance == null || GamePersistence.Instance.SaveNow());
        if (!saved)
        {
            manager.RestoreResources(before);
            manager.RestoreFirstFiveStarReward(milestone);
            claimed = false; rewardPrepared = true; RefreshButton();
            Debug.LogWarning("Claim could not be granted/saved; reward remains pending.", this);
            return;
        }
        Debug.Log($"[CLAIM] +{credit.coins} Coins, +{credit.energy} Energy, +{credit.resortValue} Resort Value, " +
            $"+{credit.materials} Materials, +{credit.gems} Gems. Materials: {manager.GetResource(ResourceType.Materials)}");
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
            claimButton.interactable = vipDisplayOnlyMode || (rewardPrepared && !claimed);
    }

}
