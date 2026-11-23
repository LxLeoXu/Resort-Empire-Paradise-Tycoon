using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public partial class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    public const int MaxEnergy = 50;

    [Header("Starting Resources")]
    [SerializeField] private int startingCoins = 12450;
    [SerializeField] private int startingEnergy = 45;
    [SerializeField] private int startingReputation = 0;

    [FormerlySerializedAs("startingWood")]
    [SerializeField] private int startingMaterials = 50;

    [SerializeField] private int startingStone = 50;
    [SerializeField] private int startingGems = 0;
    [SerializeField] private int startingResortValue = 700;

    [Header("First ordinary 5-star wave (provisional amount; team approval required)")]
    [SerializeField, Min(1)] private int firstFiveStarGemReward = 10;
    private FirstFiveStarRewardSave firstFiveStarReward = new();
    public bool FirstFiveStarRewardClaimed => firstFiveStarReward.claimed;
    public int GetFirstFiveStarGemReward(int waveId, int stars, bool pendingBonus) =>
        waveId > 0 && stars == 5 && !pendingBonus && !FirstFiveStarRewardClaimed ? Mathf.Max(1, firstFiveStarGemReward) : 0;
    public void MarkFirstFiveStarRewardClaimed() => firstFiveStarReward.claimed = true;
    public FirstFiveStarRewardSave CaptureFirstFiveStarReward() => new()
        { claimed = firstFiveStarReward.claimed, legacyHistoryUnknown = firstFiveStarReward.legacyHistoryUnknown };
    public void RestoreFirstFiveStarReward(FirstFiveStarRewardSave saved)
    {
        // Legacy history cannot distinguish a past 5-star reward from starting/daily Gems.
        // Preserve the wallet and do not issue an unprovable duplicate milestone.
        firstFiveStarReward = saved == null ? new FirstFiveStarRewardSave { claimed = true, legacyHistoryUnknown = true }
            : new FirstFiveStarRewardSave { claimed = saved.claimed, legacyHistoryUnknown = saved.legacyHistoryUnknown };
    }

    private readonly Dictionary<ResourceType, int> resources = new();

    public event Action<ResourceType, int> OnResourceChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        RestoreResources(CaptureStartingResources());
    }

    public List<ResourceSave> CaptureStartingResources()
    {
        return new List<ResourceSave>
        {
            new ResourceSave
            {
                type = ResourceType.Coins,
                amount = Mathf.Max(0, startingCoins)
            },
            new ResourceSave
            {
                type = ResourceType.Energy,
                amount = Mathf.Clamp(startingEnergy, 0, MaxEnergy)
            },
            new ResourceSave
            {
                type = ResourceType.Reputation,
                amount = Mathf.Max(0, startingReputation)
            },
            new ResourceSave
            {
                type = ResourceType.Materials,
                amount = Mathf.Max(0, startingMaterials)
            },
            new ResourceSave
            {
                type = ResourceType.Stone,
                amount = Mathf.Max(0, startingStone)
            },
            new ResourceSave
            {
                type = ResourceType.Gems,
                amount = Mathf.Max(0, startingGems)
            },
            new ResourceSave
            {
                type = ResourceType.ResortValue,
                amount = Mathf.Max(0, startingResortValue)
            }
        };
    }

    public int GetResource(ResourceType type)
    {
        return resources.TryGetValue(type, out int amount)
            ? amount
            : 0;
    }

    public bool AddResource(ResourceType type, int amount)
    {
        if (amount <= 0)
            return false;

        int current = GetResource(type);
        int newAmount;

        if (type == ResourceType.Energy)
        {
            newAmount = (int)Math.Min(
                (long)current + amount,
                MaxEnergy);
        }
        else
        {
            if (amount > int.MaxValue - current)
                return false;

            newAmount = current + amount;
        }

        if (newAmount == current)
            return false;

        resources[type] = newAmount;

        OnResourceChanged?.Invoke(type, newAmount);

        return true;
    }

    public bool SpendResource(ResourceType type, int amount)
    {
        if (type == ResourceType.ResortValue ||
            type == ResourceType.Reputation)
            return false;

        if (amount <= 0 || GetResource(type) < amount)
            return false;

        resources[type] -= amount;

        OnResourceChanged?.Invoke(type, resources[type]);

        return true;
    }

    public bool HasEnough(ResourceType type, int amount)
    {
        return amount >= 0 && GetResource(type) >= amount;
    }
}