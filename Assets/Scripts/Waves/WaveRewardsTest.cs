
using UnityEngine;

public class WaveRewardsTest : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ClaimRewards claimRewards;

    [Header("Test Wave")]
    [SerializeField] private int testWaveId = 1;
    [SerializeField] private int testGuests = 12;
    [SerializeField, Range(0, 100)]
    private int testSatisfaction = 88;

    private ResourceManager manager;
    private WaveRewardsData rewards;

    private int coinsBefore;
    private int energyBefore;
    private int resortBefore;

    private void Start()
    {
        manager = ResourceManager.Instance;

        if (manager == null || claimRewards == null)
        {
            Debug.LogError("Chyba: ResourceManager alebo ClaimRewards chyba!");
            return;
        }

        coinsBefore = manager.GetResource(ResourceType.Coins);
        energyBefore = manager.GetResource(ResourceType.Energy);
        resortBefore = manager.GetResource(ResourceType.ResortValue);

        rewards = WaveRewardCalculator.CalculateRewards(
            testGuests, testSatisfaction);

        claimRewards.OnClaimed += VerifyRewards;

        bool prepared = claimRewards.SetRewardsForWave(
            testWaveId, rewards);

        if (!prepared)
        {
            Debug.LogError("TEST FAIL: Odmena sa nepripravila. " +
                           "Skontroluj ine testery alebo ID vlny.");
            return;
        }

        Debug.Log("=== WAVE REWARDS TEST ===");
        Debug.Log("Wave ID: " + testWaveId);
        Debug.Log("Guests: " + testGuests);
        Debug.Log("Satisfaction: " + testSatisfaction + "%");

        Debug.Log("Coins: +" + rewards.coins);
        Debug.Log("Energy: +" + rewards.energy);
        Debug.Log("Resort Value: +" + rewards.resortValue);

        Debug.Log("Teraz klikni na Claim Rewards.");
    }

    private void VerifyRewards()
    {
        int expectedCoins = coinsBefore + rewards.coins;

        int expectedEnergy = Mathf.Min(
            ResourceManager.MaxEnergy,
            energyBefore + rewards.energy);

        int expectedResort = resortBefore + rewards.resortValue;

        bool coinsOK = manager.GetResource(ResourceType.Coins)
            == expectedCoins;

        bool energyOK = manager.GetResource(ResourceType.Energy)
            == expectedEnergy;

        bool resortOK = manager.GetResource(ResourceType.ResortValue)
            == expectedResort;

        bool duplicateBlocked = !claimRewards.SetRewardsForWave(
            testWaveId, rewards);

        Debug.Log("Coins: " + (coinsOK ? "PASS" : "FAIL"));
        Debug.Log("Energy: " + (energyOK ? "PASS" : "FAIL"));
        Debug.Log("Resort Value: " + (resortOK ? "PASS" : "FAIL"));
        Debug.Log("Duplicate Wave: " +
                  (duplicateBlocked ? "PASS" : "FAIL"));

        if (coinsOK && energyOK && resortOK && duplicateBlocked)
            Debug.Log("=== ALL TESTS PASSED ===");
        else
            Debug.LogError("=== TEST FAILED ===");
    }

    private void OnDestroy()
    {
        if (claimRewards != null)
            claimRewards.OnClaimed -= VerifyRewards;
    }
}
