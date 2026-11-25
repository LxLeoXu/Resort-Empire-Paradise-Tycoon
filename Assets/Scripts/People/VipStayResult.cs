using System;
using System.Collections.Generic;

[Serializable]
public sealed class VipStayResult
{
    public string guestName;
    public int requiredCompleted, stars;
    public bool bonusCompleted, perfectStay;
    public List<string> requirements = new();
    public WaveRewardsData rewards;

    // Provisional team-leader approval: 0/1/2/3 required -> 1/2/3/4 stars;
    // all three required plus the bonus -> 5 stars. Energy is never consulted.
    public static VipStayResult Create(VipGuestDefinition definition, ICollection<string> fulfilled, bool accepted)
    {
        if (definition == null || !definition.IsValid() || fulfilled == null)
            throw new ArgumentException("Invalid VIP result inputs.");
        var result = new VipStayResult { guestName = definition.displayName, rewards = new WaveRewardsData(0, 0, 0) };
        foreach (var requirement in definition.requirements)
        {
            bool done = fulfilled.Contains(requirement.id);
            result.requirements.Add((done ? "[x] " : "[ ] ") + requirement.description);
            if (requirement.bonus) result.bonusCompleted = done;
            else if (done) result.requiredCompleted++;
        }
        result.perfectStay = result.requiredCompleted == 3 && result.bonusCompleted;
        result.stars = result.perfectStay ? 5 : result.requiredCompleted + 1;
        if (accepted)
        {
            if (result.requiredCompleted != 3) throw new InvalidOperationException("Required VIP requests are incomplete.");
            AddRewards(result.rewards, definition.rewards);
            if (result.bonusCompleted) AddRewards(result.rewards, definition.bonusRewards);
        }
        return result;
    }
    private static void AddRewards(WaveRewardsData target, List<VipReward> rewards)
    {
        checked
        {
            foreach (var reward in rewards)
                switch (reward.resource)
                {
                    case ResourceType.Reputation: target.reputation += reward.amount; break;
                    case ResourceType.Coins: target.coins += reward.amount; break;
                    case ResourceType.Energy: target.energy += reward.amount; break;
                    case ResourceType.Materials: target.materials += reward.amount; break;
                    case ResourceType.ResortValue: target.resortValue += reward.amount; break;
                    default: throw new InvalidOperationException("Unsupported S8 reward: " + reward.resource);
                }
        }
    }
    public bool IsValid() => !string.IsNullOrWhiteSpace(guestName) && requiredCompleted >= 0 && requiredCompleted <= 3 &&
        perfectStay == (requiredCompleted == 3 && bonusCompleted) && stars == (perfectStay ? 5 : requiredCompleted + 1) &&
        requirements != null && requirements.Count == 4 && requirements.TrueForAll(s => !string.IsNullOrWhiteSpace(s)) &&
        rewards != null && rewards.IsValid() && rewards.gems == 0 && !rewards.firstFiveStarBonus;
}
