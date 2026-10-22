
using System;
using UnityEngine;

public class ResourceTester : MonoBehaviour
{
    private ResourceManager manager;

    private void Start()
    {
        manager = ResourceManager.Instance;

        if (manager == null)
        {
            Debug.LogError("ResourceManager neexistuje!");
            enabled = false;
            return;
        }

        manager.OnResourceChanged += OnResourceChanged;

        Debug.Log("=== POCIATOCNE ZDROJE ===");
        PrintAllResources();
        Debug.Log("=== TESTOVANIE ZDROJOV ===");

        TestResources();
    }

    private void TestResources()
    {
        // Test pridavania
        Debug.Log("--- PRIDAVANIE ---");

        TestAdd(ResourceType.Coins, 100);
        TestAdd(ResourceType.Energy, 10);
        TestAdd(ResourceType.Reputation, 5);
        TestAdd(ResourceType.Wood, 20);
        TestAdd(ResourceType.Stone, 15);
        TestAdd(ResourceType.Gems, 2);
        TestAdd(ResourceType.ResortValue, 100);

        // Test odoberania
        Debug.Log("--- ODOBERANIE ---");

        TestSpend(ResourceType.Coins, 50);
        TestSpend(ResourceType.Energy, 5);
        TestSpend(ResourceType.Reputation, 2);
        TestSpend(ResourceType.Wood, 10);
        TestSpend(ResourceType.Stone, 5);
        TestSpend(ResourceType.Gems, 1);

        // Hodnota rezortu sa nesmie dať znížiť
        TestSpendRefused(ResourceType.ResortValue, 50);

        // Test nedostatku zdrojov
        Debug.Log("--- NEDOSTATOK ZDROJOV ---");

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            int before = manager.GetResource(type);

            bool success = manager.SpendResource(type, int.MaxValue);

            int after = manager.GetResource(type);

            bool passed = !success && before == after;

            Debug.Log(
                $"{(passed ? "PASS" : "FAIL")} " +
                $"{type}: Nedostatok zdrojov, " +
                $"Pred={before}, Po={after}"
            );
        }

        Debug.Log("=== KONECNY STAV ZDROJOV ===");
        PrintAllResources();
    }

    private void TestAdd(ResourceType type, int amount)
    {
        int before = manager.GetResource(type);

        bool success = manager.AddResource(type, amount);

        int after = manager.GetResource(type);

        bool passed = success && after == before + amount;

        Debug.Log(
            $"{(passed ? "PASS" : "FAIL")} " +
            $"{type}: +{amount}, " +
            $"Pred={before}, Po={after}"
        );
    }

    private void TestSpend(ResourceType type, int amount)
    {
        int before = manager.GetResource(type);

        bool success = manager.SpendResource(type, amount);

        int after = manager.GetResource(type);

        bool passed = success && after == before - amount;

        Debug.Log(
            $"{(passed ? "PASS" : "FAIL")} " +
            $"{type}: -{amount}, " +
            $"Pred={before}, Po={after}"
        );
    }

    private void TestSpendRefused(ResourceType type, int amount)
    {
        int before = manager.GetResource(type);

        bool success = manager.SpendResource(type, amount);

        int after = manager.GetResource(type);

        bool passed = !success && after == before;

        Debug.Log(
            $"{(passed ? "PASS" : "FAIL")} " +
            $"{type}: -{amount} zamietnuté, " +
            $"Pred={before}, Po={after}"
        );
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        Debug.Log($"EVENT: {type} sa zmenil na {amount}");
    }

    private void PrintAllResources()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            Debug.Log($"{type}: {manager.GetResource(type)}");
        }
    }

    private void OnDestroy()
    {
        if (manager != null)
            manager.OnResourceChanged -= OnResourceChanged;
    }
}
