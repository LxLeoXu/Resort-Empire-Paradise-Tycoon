using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class VipRequirement
{
    public string id;
    public string description;
    public bool bonus;
    [Min(0)] public int materials = 10;

    [Header("S7 Vizuál a časovanie")]
    public NeedType needType = NeedType.Drinks;
    [Tooltip("Ak je prázdne, názov miesta sa doplní automaticky podľa Need Type.")]
    public string locationName;
    [Tooltip("Voliteľná vlastná ikona. Ak je prázdna, použije sa ikona podľa Need Type.")]
    public Sprite icon;
    [Tooltip("Zaškrtni, ak požiadavka nevyžaduje Fix a začína rovno ako READY.")]
    public bool startsReady = false;
    [Min(1f)] public float serveDurationSeconds = 12f;
}

[Serializable]
public sealed class VipReward
{
    public ResourceType resource = ResourceType.Coins;
    [Min(1)] public int amount = 100;
}

[CreateAssetMenu(menuName = "Paradise Tycoon/VIP Guest")]
public sealed class VipGuestDefinition : ScriptableObject
{
    public string guestId = "travel-blogger";
    public string displayName = "Travel Blogger";
    public Sprite portrait;
    public List<VipRequirement> requirements = new();
    public List<VipReward> rewards = new();
    public List<VipReward> bonusRewards = new();

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(guestId) ||
            string.IsNullOrWhiteSpace(displayName) ||
            requirements == null ||
            requirements.Count != 4 ||
            rewards == null ||
            bonusRewards == null)
            return false;

        int required = 0, bonus = 0;
        var ids = new HashSet<string>();

        foreach (var r in requirements)
        {
            if (r == null || string.IsNullOrWhiteSpace(r.id) || !ids.Add(r.id))
                return false;

            if (!r.startsReady && r.materials <= 0)
                return false;

            if (r.bonus) bonus++;
            else required++;
        }

        foreach (var r in rewards)
            if (r == null || r.amount <= 0 || !Enum.IsDefined(typeof(ResourceType), r.resource))
                return false;

        foreach (var r in bonusRewards)
            if (r == null || r.amount <= 0 || !Enum.IsDefined(typeof(ResourceType), r.resource))
                return false;

        return required == 3 && bonus == 1;
    }
}