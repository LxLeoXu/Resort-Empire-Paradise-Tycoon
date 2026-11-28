using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Resort/Manager Catalog")]
public sealed class ManagerCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string displayName;
        public NeedType buildingNeed;
        [Min(1)] public int coinPrice = 500;
        [Min(1f)] public float speedMultiplier = 1.2f;
    }

    [SerializeField] private Entry[] managers = Array.Empty<Entry>();

    public Entry ForBuilding(NeedType need)
    {
        foreach (var entry in managers)
            if (entry != null && entry.buildingNeed == need) return entry;
        return null;
    }
}
