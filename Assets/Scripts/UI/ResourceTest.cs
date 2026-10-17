
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
}
