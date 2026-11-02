
using UnityEngine;

public class ResourceTest : MonoBehaviour
{
    public void AddEnergy()
    {
        ResourceManager.Instance.AddResource(ResourceType.Energy, 10);
    }

    public void AddReputation()
    {
        ResourceManager.Instance.AddResource(ResourceType.Reputation, 5);
    }


    public void AddResortValue()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager neexistuje!");
            return;
        }

        ResourceManager.Instance.AddResource(
            ResourceType.ResortValue, 100
        );

        Debug.Log("Aktualna hodnota rezortu: " +
            ResourceManager.Instance.GetResource(
                ResourceType.ResortValue));
    }

}

