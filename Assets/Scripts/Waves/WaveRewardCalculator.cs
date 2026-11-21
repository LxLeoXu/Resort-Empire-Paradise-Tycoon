using UnityEngine;

public static class WaveRewardCalculator
{
    public static int GetStarRating(int satisfactionPercent)
    {
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);

        if (satisfaction >= 90) return 5;
        if (satisfaction >= 80) return 4;
        if (satisfaction >= 70) return 3;
        if (satisfaction >= 55) return 2;

        return 1;
    }

    public static int CalculateMaterials(int satisfactionPercent)
    {
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);
        int stars = GetStarRating(satisfaction);

        if (stars < 4) return 0;
        if (stars == 5) return 3;
        if (satisfaction >= 85) return 2;

        return 1;
    }

    public static WaveRewardsData CalculateRewards(
        int servedGuests,
        int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);
        int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);

        int stars = GetStarRating(satisfaction);

        const int coinsPerGuest = 16;

        float coinMultiplier = stars == 1
            ? 0.4f
            : satisfaction / 100f;

        int coins = Mathf.RoundToInt(
            guests * coinsPerGuest * coinMultiplier);

        int energy = Mathf.Clamp(
            Mathf.RoundToInt(satisfaction / 20f),
            0, 5);

        int resortValue = Mathf.RoundToInt(guests * 1.25f);

        int materials = CalculateMaterials(satisfaction);

        int reputation = satisfaction / 10;

        return new WaveRewardsData(
            coins,
            energy,
            resortValue,
            materials,
            reputation);
    }
}