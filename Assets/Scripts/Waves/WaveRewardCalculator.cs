using UnityEngine;

public static class WaveRewardCalculator
{
    // SCRUM-399: Energy odmeny podla hviezdiciek
    // 1★ = 0, 2★ = 1, 3★ = 2, 4★ = 3, 5★ = 5
    private static readonly int[] EnergyByStars =
    {
        0, // 0★
        0, // 1★
        1, // 2★
        2, // 3★
        3, // 4★
        5  // 5★
    };

    // Hodnotenie vlny podla spokojnosti
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

    // SCRUM-363: Vypocet Energy za dokoncenu vlnu
    public static int CalculateEnergy(int satisfactionPercent)
    {
        int stars = GetStarRating(satisfactionPercent);

        return EnergyByStars[stars];
    }
    // SCRUM-532: Jednotny vypocet Coins pre S4 aj Claim Rewards.
public const int CoinsPerNeed = 16;

public static float GetCoinMultiplier(int satisfactionPercent)
{
    int satisfaction = Mathf.Clamp(satisfactionPercent, 0, 100);
    int stars = GetStarRating(satisfaction);

    return stars == 1
        ? 0.4f
        : satisfaction / 100f;
}

public static int CalculateCoins(
    int servedNeeds,
    int satisfactionPercent)
{
    int needs = Mathf.Max(0, servedNeeds);

    return Mathf.RoundToInt(
        needs * CoinsPerNeed *
        GetCoinMultiplier(satisfactionPercent));
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

        // Za 5 hviezdiciek
        if (stars == 5)
            return 3;

        // Za 4 hviezdicky
        if (satisfaction >= 85)
            return 2;

        return 1;
    }

    // Vypocet odmien za dokoncenu vlnu
    public static WaveRewardsData CalculateRewards(
        int servedGuests,
        int satisfactionPercent)
    {
        int guests = Mathf.Max(0, servedGuests);

        int satisfaction = Mathf.Clamp(
            satisfactionPercent, 0, 100);

        int stars = GetStarRating(satisfaction);

        // SCRUM-532: Coins sa pocitaju na jednom mieste.
int coins = CalculateCoins(guests, satisfaction);
        // SCRUM-363: Energy podla hviezdiciek
        int energy = CalculateEnergy(satisfaction);

        // Resort Value
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