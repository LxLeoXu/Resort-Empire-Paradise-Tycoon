using UnityEngine;

[System.Serializable]
public class WaveRewardsData
{
    public int coins;
    public int energy;
    public int resortValue;
    public int materials;
    public int reputation;

    public WaveRewardsData(
        int coins,
        int energy,
        int resortValue,
        int materials = 0,
        int reputation = 0)
    {
        this.coins = coins;
        this.energy = energy;
        this.resortValue = resortValue;
        this.materials = materials;
        this.reputation = reputation;
    }
}