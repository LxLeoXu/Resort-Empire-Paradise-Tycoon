using UnityEngine;

[System.Serializable]
public class WaveRewardsData
{
    public int coins;
    public int energy;
    public int resortValue;

    // SCRUM-438: Materialy za uspesnu vlnu
    public int materials;

    public WaveRewardsData(
        int coins,
        int energy,
        int resortValue,
        int materials = 0)
    {
        this.coins = coins;
        this.energy = energy;
        this.resortValue = resortValue;
        this.materials = materials;
    }
}