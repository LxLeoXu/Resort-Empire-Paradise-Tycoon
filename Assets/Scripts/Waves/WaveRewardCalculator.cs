
using UnityEngine;

public static class WaveRewardCalculator
{
    // SCRUM-306: Minimalne 1 hviezdicka
    public static int GetStarRating(int satisfactionPercent)
    {
        int satisfaction = Mathf.Clamp(
            satisfactionPercent, 0, 100);

        if (satisfaction >= 90) return 5;
        if (satisfaction >= 80) return 4;
        if (satisfaction >= 70) return 3;
        if (satisfaction >= 55) return 2;

        return 1;
    }

    // SCRUM-307: Minimalna odmena pri 1 hviezdicke
    public static WaveRewardsData CalculateRewards(
        int servedGuests,
        int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);

        int satisfaction = Mathf.Clamp(
            satisfactionPercent, 0, 100);

        int stars = GetStarRating(satisfaction);

        const int coinsPerGuest = 10;

        // Pri 1 hviezdicke je nasobok 0.4
        // Pri ostatnych ponechame povodny vypocet.
        float coinMultiplier = stars == 1
            ? 0.4f
            : satisfaction / 100f;

        int coins = Mathf.RoundToInt(
            guests * coinsPerGuest * coinMultiplier);

        // Povodny vypocet energie
        int energy = Mathf.Clamp(
            Mathf.RoundToInt(satisfaction / 20f),
            0, 5);

        // Povodny vypocet hodnoty rezortu
        int resortValue = Mathf.RoundToInt(
            guests * 1.25f);

        return new WaveRewardsData(
            coins,
            energy,
            resortValue);
    }
}
