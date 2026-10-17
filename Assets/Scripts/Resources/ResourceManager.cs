
using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    public const int MaxEnergy = 50;

    [Header("Starting Resources")]
    [SerializeField] private int startingCoins = 12450;
    [SerializeField] private int startingEnergy = 45;
    [SerializeField] private int startingReputation = 0;
    [SerializeField] private int startingWood = 50;
    [SerializeField] private int startingStone = 50;
    [SerializeField] private int startingGems = 10;
    [SerializeField] private int startingResortValue = 700;

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

        resources[ResourceType.Coins] = Mathf.Max(0, startingCoins);
        resources[ResourceType.Energy] =
            Mathf.Clamp(startingEnergy, 0, MaxEnergy);
        resources[ResourceType.Reputation] = Mathf.Max(0, startingReputation);
        resources[ResourceType.Wood] = Mathf.Max(0, startingWood);
        resources[ResourceType.Stone] = Mathf.Max(0, startingStone);
        resources[ResourceType.Gems] = Mathf.Max(0, startingGems);
        resources[ResourceType.ResortValue] = Mathf.Max(0, startingResortValue);
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
                (long)current + amount, MaxEnergy);
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
