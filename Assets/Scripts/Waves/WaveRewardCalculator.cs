using UnityEngine;

public static class WaveRewardCalculator
{
    // Minimalne 1 hviezdicka
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

    // SCRUM-438: Materialy za 4 a 5 hviezdiciek
    public static int CalculateMaterials(int satisfactionPercent)
    {
        int satisfaction = Mathf.Clamp(
            satisfactionPercent, 0, 100);

        int stars = GetStarRating(satisfaction);

        // Za 1 az 3 hviezdicky ziadne materialy
        if (stars < 4)
            return 0;

        // 5 hviezdiciek
        if (stars == 5)
            return 3;

        // 4 hviezdicky
        if (satisfaction >= 85)
            return 2;

        return 1;
    }

    public static WaveRewardsData CalculateRewards(
        int servedGuests,
        int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);

        int satisfaction = Mathf.Clamp(
            satisfactionPercent, 0, 100);

        int stars = GetStarRating(satisfaction);

        const int coinsPerGuest = 10;

        // Povodny vypocet Coins
        float coinMultiplier = stars == 1
            ? 0.4f
            : satisfaction / 100f;

        int coins = Mathf.RoundToInt(
            guests * coinsPerGuest * coinMultiplier);

        // Povodny vypocet Energy
        int energy = Mathf.Clamp(
            Mathf.RoundToInt(satisfaction / 20f),
            0, 5);

        // Povodny vypocet Resort Value
        int resortValue = Mathf.RoundToInt(
            guests * 1.25f);

        // SCRUM-438: Materialy
        int materials = CalculateMaterials(satisfaction);

        return new WaveRewardsData(
            coins,
            energy,
            resortValue,
            materials);
    }
}