using UnityEngine;

[System.Serializable]
public class WaveRewardsData
{
    public int coins;
    public int energy;
    public int resortValue;

    // SCRUM-438: Materialy za uspesnu vlnu
    public int materials;
    public int reputation;
    public int gems;
    public bool firstFiveStarBonus;

    public bool IsValid() => coins >= 0 && energy >= 0 && resortValue >= 0 && materials >= 0 &&
        reputation >= 0 && gems >= 0 && (!firstFiveStarBonus || gems > 0);
    public WaveRewardsData Copy() => new(coins, energy, resortValue, materials, reputation, gems, firstFiveStarBonus);

    public WaveRewardsData(
        int coins,
        int energy,
        int resortValue,
        int materials = 0,
        int reputation = 0,
        int gems = 0,
        bool firstFiveStarBonus = false)
    {
        this.coins = coins;
        this.energy = energy;
        this.resortValue = resortValue;
        this.materials = materials;
        this.reputation = reputation;
        this.gems = gems;
        this.firstFiveStarBonus = firstFiveStarBonus;
    }
}