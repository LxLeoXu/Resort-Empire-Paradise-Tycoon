using System;

[Serializable]
public class WaveRewardsData
{
    public int coins;
    public int energy;
    public int resortValue;

    public WaveRewardsData(int coins, int energy, int resortValue)
    {
        this.coins = Math.Max(0, coins);
        this.energy = Math.Max(0, energy);
        this.resortValue = Math.Max(0, resortValue);
    }
}
