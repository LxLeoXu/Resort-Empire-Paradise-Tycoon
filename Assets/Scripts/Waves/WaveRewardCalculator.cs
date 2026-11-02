using UnityEngine;

public static class WaveRewardCalculator
{
    public static WaveRewardsData CalculateRewards(
        int servedGuests,
        int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);

        int coins = Mathf.RoundToInt(guests * 10 * (satisfaction / 100f));

        int energy = Mathf.Clamp(Mathf.RoundToInt(satisfaction / 20f), 0, 5);

        int resortValue = Mathf.RoundToInt(guests * 1.25f);

        return new WaveRewardsData(coins, energy, resortValue);
    }
}