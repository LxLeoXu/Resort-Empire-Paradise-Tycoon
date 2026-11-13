using UnityEngine;

[System.Serializable]
public class WaveRewardsData
{
    public int coins;
    public int energy;
    public int resortValue;

    public WaveRewardsData(int coins, int energy, int resortValue)
    {
        this.coins = coins;
        this.energy = energy;
        this.resortValue = resortValue;
    }
}