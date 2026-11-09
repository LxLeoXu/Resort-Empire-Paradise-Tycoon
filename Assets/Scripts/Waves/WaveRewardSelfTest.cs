
using UnityEngine;

public class WaveRewardSelfTest : MonoBehaviour
{
    [ContextMenu("Run SCRUM-93 Tests")]
    public void RunTests()
    {
        int passed = 0;
        int failed = 0;

        Check("0% = 1 star",
            WaveRewardCalculator.GetStarRating(0) == 1,
            ref passed, ref failed);

        Check("35% = 1 star",
            WaveRewardCalculator.GetStarRating(35) == 1,
            ref passed, ref failed);

        Check("54% = 1 star",
            WaveRewardCalculator.GetStarRating(54) == 1,
            ref passed, ref failed);

        Check("55% = 2 stars",
            WaveRewardCalculator.GetStarRating(55) == 2,
            ref passed, ref failed);

        Check("80% = 4 stars",
            WaveRewardCalculator.GetStarRating(80) == 4,
            ref passed, ref failed);

        Check("90% = 5 stars",
            WaveRewardCalculator.GetStarRating(90) == 5,
            ref passed, ref failed);

        WaveRewardsData lowReward =
            WaveRewardCalculator.CalculateRewards(12, 35);

        Check("1 star reward = 48 coins",
            lowReward.coins == 48,
            ref passed, ref failed);

        WaveRewardsData normalReward =
            WaveRewardCalculator.CalculateRewards(12, 55);

        Check("2 stars reward = 66 coins",
            normalReward.coins == 66,
            ref passed, ref failed);

        Debug.Log(
            $"SCRUM-93 RESULTS: {passed} PASS, {failed} FAIL");
    }

    private void Check(
        string name,
        bool condition,
        ref int passed,
        ref int failed)
    {
        if (condition)
        {
            passed++;
            Debug.Log("PASS: " + name);
        }
        else
        {
            failed++;
            Debug.LogError("FAIL: " + name);
        }
    }
}
