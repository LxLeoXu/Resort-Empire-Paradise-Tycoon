using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

[CreateAssetMenu(menuName = "Resort/Manager Catalog")]
public sealed class ManagerCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Role name key in the GameTexts string table.")]
        public string displayName;
        public NeedType buildingNeed;
        [Min(1)] public int coinPrice = 500;
        [Min(1f)] public float speedMultiplier = 1.2f;

        public string GetHireLabel(bool hired)
        {
            var strings = LocalizationSettings.StringDatabase;
            string role = strings.GetLocalizedString("GameTexts", displayName);
            return strings.GetLocalizedString("GameTexts", hired ? "s3_manager_hired" : "s3_manager_hire",
                new object[] { role, speedMultiplier, coinPrice });
        }
    }

    [SerializeField] private Entry[] managers = Array.Empty<Entry>();

    public Entry ForBuilding(NeedType need)
    {
        foreach (var entry in managers)
            if (entry != null && entry.buildingNeed == need) return entry;
        return null;
    }
}
