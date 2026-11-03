using System;
using UnityEngine;

public static class WaveRewardCalculator
{
    // DOCASNE pravidla na otestovanie. Potvrd s timom pred uzavretim Jira ulohy.
    private const int CoinsPerGuest = 10;

    // Povodna metoda zostava pre kompatibilitu so starsimi skriptmi.
    public static int CalculateReward(int servedGuests, int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);
        long scaled = (long)guests * CoinsPerGuest * satisfaction;
        return (int)Math.Min((scaled + 50L) / 100L, int.MaxValue);
    }

    public static WaveRewardsData CalculateRewards(int servedGuests, int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);

        int coins = CalculateReward(guests, satisfaction);
        int energy = Mathf.Clamp(Mathf.RoundToInt(satisfaction / 20f), 0, 5);
        int resortValue = (int)Math.Min(((long)guests * 5L + 2L) / 4L, int.MaxValue);

        return new WaveRewardsData(coins, energy, resortValue);
    }
}
